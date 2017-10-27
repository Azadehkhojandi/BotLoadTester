using System;

namespace BotPerformanceTester
{
    public class ResultItem
    {
        public ResultItem()
        {
            Date=DateTime.Now;
            Ticks = DateTime.Now.Ticks;
        }
        public int ThreadId { get; set; }
        public string Title { get; set; }
        public int Elapsed { get; set; }
        public string Message { get; set; }
        public string RecivedId { get; set; }
        public string ConversationId { get; set; }
        public DateTime Date { get; set; }
        public long Ticks { get; set; }
        public string From { get; set; }
        public string Status { get; set; }
    }
}