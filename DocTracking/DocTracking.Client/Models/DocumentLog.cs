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

        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        public int OfficeId { get; set; }

        [ForeignKey("OfficeId")]
        public Office? Office { get; set; }

        public string? Action { get; set; }

    }
}
