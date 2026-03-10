namespace DocTracking.Client.Models
{
    public class AppNotification
    {
        public string Message { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }
}
