using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;

namespace BotPerformanceTester
{
    public class Program
    {
        private static readonly string _directLineSecret = ConfigurationManager.AppSettings["DirectLineSecret"];
        private static readonly string _botId = ConfigurationManager.AppSettings["BotId"];
        private static readonly string _fromUserPrefix = "DirectLineSampleClientUser";
        private static List<ResultItem> _result = new List<ResultItem>();
        private static Dictionary<string,ConsoleColor> _converstaionColors=new Dictionary<string, ConsoleColor>();

        private static int _concurrentConversations = 20;
        private static int _messageInEachConversation = 10;
        private static bool _exporting = false;

        private static List<Thread> _threads = new List<Thread>();

        public static void Main(string[] args)
        {
            Console.WriteLine("******** Bot Performance tester ********");
            Console.WriteLine($"Number of concurrent conversations: {_concurrentConversations} ");
            Console.WriteLine($"Number of messages in each conversation: {_messageInEachConversation}");

            if (string.IsNullOrEmpty(_directLineSecret)|| string.IsNullOrEmpty(_botId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Please set DirectLineSecret & BotId values in app.cofig");
                Console.ReadLine();
                return;
            }

            StartBotConversation().Wait();


            Console.ReadLine();
            _exporting = true;

            foreach (var thread in _threads)
            {
                thread.Abort();
            }


            var filename = $"{Guid.NewGuid().ToString()}.csv";


            Console.WriteLine($"Exporting result to excel - {filename}");




            //write results to csv file
            using (TextWriter writer = new StreamWriter(filename))
            {
                var csv = new CsvWriter(writer);

                csv.WriteRecords(_result);
            }

            Console.WriteLine($"************** csv file exported till the time you pressed enter **************");
         
            

        }

        private static async Task StartBotConversation()
        {

            var random = new Random();
           

           


            var colors = Enum.GetValues(typeof(ConsoleColor)).Cast<ConsoleColor>()
                .Where(x => x != ConsoleColor.Black).ToArray();

        
          


            var testmessages = new[] { "This is a random message", "Microsoft loves developers" };
            var stopWatch = new Stopwatch();


            for (var i = 0; i < _concurrentConversations; i++)
            {

                if (_exporting)
                {
                    break;
                }

                Thread senderThread = null;
                Thread receiverThread = null;

                var client = new DirectLineClient(_directLineSecret);
                var conversation = await client.Conversations.StartConversationAsync();

                var color = (ConsoleColor)colors.GetValue(random.Next(colors.Length));
                _converstaionColors.Add(conversation.ConversationId,color);
                //each converstaion in different thread
                senderThread = new System.Threading.Thread(async () =>

                {
                   
                    //generate messages
                    var fromUser = _fromUserPrefix + Guid.NewGuid();

                    for (var j = 0; j < _messageInEachConversation; j++)
                    {
                        

                        var rnd = random.Next(0, testmessages.Length);

                        var userMessage = new Activity
                        {
                            From = new ChannelAccount(fromUser),
                            Text = $"{testmessages[rnd]}-{Guid.NewGuid().ToString()}",
                            Type = ActivityTypes.Message
                        };

                        Console.ForegroundColor = _converstaionColors[conversation.ConversationId];
                        Console.WriteLine( $"Thread ID {Thread.CurrentThread.ManagedThreadId} \t {conversation.ConversationId} \t sending {userMessage.Text}");

                        _result.Add(new ResultItem()
                        {
                            Elapsed = stopWatch.Elapsed.Milliseconds,
                            Title = "Sender",
                            Status = "Start Sending",
                            From = fromUser,
                            Message = userMessage.Text,
                            ThreadId = Thread.CurrentThread.ManagedThreadId,
                            RecivedId = "",
                            ConversationId = conversation.ConversationId,

                        });


                        stopWatch.Start();
                        var result = await client.Conversations.PostActivityAsync(conversation.ConversationId, userMessage);
                        stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        var ts = stopWatch.Elapsed;
                        Console.ForegroundColor = _converstaionColors[conversation.ConversationId];
                        Console.WriteLine($"Thread ID {Thread.CurrentThread.ManagedThreadId} \t {conversation.ConversationId} \t recived Id:{result?.Id}  \t Elapsed:{ts.Milliseconds} milliseconds");
                        _result.Add(new ResultItem()
                        {
                            Elapsed = stopWatch.Elapsed.Milliseconds,
                            Title = "Sender",
                            Status = "Received acknowledgement after Sending",
                            Message = "",
                            ThreadId = Thread.CurrentThread.ManagedThreadId,
                            RecivedId = result?.Id,
                            ConversationId = conversation.ConversationId
                        });
                    }
                });

                //set receiver 
                receiverThread = new System.Threading.Thread(async () =>
                {
                    await ReadBotMessagesAsync(client, conversation.ConversationId);
                });

                _threads.Add(receiverThread);
                _threads.Add(senderThread);
                receiverThread.Start();
                senderThread.Start();
              


            }


           

        }

        private static async Task ReadBotMessagesAsync(DirectLineClient client, string conversationId)
        {
            Console.ForegroundColor = _converstaionColors[conversationId];
            Console.WriteLine($"ReadBotMessagesAsync : Thread.CurrentThread.ManagedThreadId :{Thread.CurrentThread.ManagedThreadId}");
           

            string watermark = null;
            var stopWatch = new Stopwatch();

            while (true)
            {
                if (_exporting)
                {
                    break;
                }

                _result.Add(new ResultItem()
                {
                    Elapsed = stopWatch.Elapsed.Milliseconds,
                    Title = "Receiver",
                    Status = "Start getting activities",
                    Message = "",
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    RecivedId = "",
                    ConversationId = conversationId,
                    Date = DateTime.Now,
                    From = ""
                });
                stopWatch.Start();
                var activitySet = await client.Conversations.GetActivitiesAsync(conversationId, watermark);
                stopWatch.Stop();

                _result.Add(new ResultItem()
                {
                    Elapsed = stopWatch.Elapsed.Milliseconds,
                    Title = "Receiver",
                    Status = "Received acknowledgement after activities",
                    Message = "",
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    RecivedId = "",
                    ConversationId = conversationId,
                    Date = DateTime.Now,
                    From = ""
                });

                if (activitySet != null && activitySet.Activities.Any())
                {
                   
                    Console.ForegroundColor = _converstaionColors[conversationId];
                    Console.WriteLine($"Thread ID {Thread.CurrentThread.ManagedThreadId} \t   {conversationId} \t receivied activies - Elapsed:{stopWatch.Elapsed.Milliseconds} milliseconds");
                   

                    watermark = activitySet.Watermark;
                    var activities = activitySet?.Activities.Where(x => x.From.Id == _botId);


                    foreach (Activity activity in activities)
                    {
                        Console.ForegroundColor = _converstaionColors[conversationId];
                        Console.WriteLine($"Thread ID {Thread.CurrentThread.ManagedThreadId} \t   {conversationId} \t response: {activity.Text}");

                        _result.Add(new ResultItem()
                        {
                           
                            Title = "Receiver",
                            Message = activity.Text,
                            ThreadId = Thread.CurrentThread.ManagedThreadId,
                            RecivedId = "",
                            ConversationId = conversationId,
                            From = activity.ChannelId
                        });
                        
                       
                        if (activity.Attachments != null)
                        {
                            foreach (Attachment attachment in activity.Attachments)
                            {
                                switch (attachment.ContentType)
                                {
                                    case "application/vnd.microsoft.card.hero":
                                        Console.ForegroundColor = _converstaionColors[conversationId];
                                        Console.WriteLine($"Thread ID {Thread.CurrentThread.ManagedThreadId} \t   {conversationId} \t response: application/vnd.microsoft.card.hero");
                                        _result.Add(new ResultItem()
                                        {
                                            Elapsed = stopWatch.Elapsed.Milliseconds,
                                            Title = "Receiver",
                                            Message = "application/vnd.microsoft.card.hero",
                                            ThreadId = Thread.CurrentThread.ManagedThreadId,
                                            RecivedId = "",
                                            ConversationId = conversationId,
                                            Date = DateTime.Now,
                                            From = activity.ChannelId
                                        });

                                        RenderHeroCard(attachment);
                                        break;

                                    case "image/png":
                                        Console.ForegroundColor = _converstaionColors[conversationId];
                                        Console.WriteLine($"Thread ID {Thread.CurrentThread.ManagedThreadId} \t   {conversationId} \t response: image/png");
                                        Console.WriteLine($"Opening the requested image '{attachment.ContentUrl}'");
                                        _result.Add(new ResultItem()
                                        {
                                            Elapsed = stopWatch.Elapsed.Milliseconds,
                                            Title = "Receiver",
                                            Message = "image/png",
                                            ThreadId = Thread.CurrentThread.ManagedThreadId,
                                            RecivedId = "",
                                            ConversationId = conversationId,
                                            Date = DateTime.Now,
                                            From = activity.ChannelId
                                        });

                                        Process.Start(attachment.ContentUrl);
                                        break;
                                }
                            }
                        }


                    }

                }


                // await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }

        private static void RenderHeroCard(Attachment attachment)
        {
            const int width = 70;
            Func<string, string> contentLine = (content) => string.Format($"{{0, -{width}}}", string.Format("{0," + ((width + content.Length) / 2).ToString() + "}", content));

            var heroCard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());

            if (heroCard != null)
            {
                Console.WriteLine("/{0}", new string('*', width + 1));
                Console.WriteLine("*{0}*", contentLine(heroCard.Title));
                Console.WriteLine("*{0}*", new string(' ', width));
                Console.WriteLine("*{0}*", contentLine(heroCard.Text));
                Console.WriteLine("{0}/", new string('*', width + 1));
            }
        }
    }
}
