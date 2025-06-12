namespace os.Models
{
    public class HomeBundle
    {
        public List<AnnouncementModel>? AnnouncementList { get; set; }
        public List<MeetingModel>? MeetingList { get; set; }
        public int? TotalVisitorsCount { get; set; }
        public List<string>? VisitorsByMonthCount { get; set; }
    }
}
 