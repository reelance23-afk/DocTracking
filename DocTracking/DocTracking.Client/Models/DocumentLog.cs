using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocTracking.Client.Models
{
    public class DocumentLog
    {
        [Key]
        public int Id { get; set; }

        public int DocumentId { get; set; }

        [ForeignKey("DocumentId")]
        public Document? Document { get; set; }

        public int? AppUserId { get; set; }

        [ForeignKey("AppUserId")]
        public AppUser? AppUser { get; set; }

        public int? OfficeId { get; set; }

        [ForeignKey("OfficeId")]
        public Office? Office { get; set; }

        public int? UnitId { get; set; }

        [ForeignKey("UnitId")]
        public Unit? Unit { get; set; }

        public string? Action { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        public string? UnitName { get; set; } 
        public string? OfficeName { get; set; }
        public string? UserName { get; set; }

        public string? Comment { get; set; }
    }
}
