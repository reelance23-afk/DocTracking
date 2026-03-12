
using System.ComponentModel.DataAnnotations;

namespace DocTracking.Client.Models
{
    public class AppNotification
    {
        [Key]
        public int Id { get; set; }
        public int AppUserId { get; set; }
        public string Message { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
    }
}
