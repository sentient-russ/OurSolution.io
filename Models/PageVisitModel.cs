using System;

namespace os.Models
{
    public class PageVisitModel
    {
        public int Id { get; set; }
        public string VisitorId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Page { get; set; }
        public string Referrer { get; set; }
        public DateTime Timestamp { get; set; }
    }
}