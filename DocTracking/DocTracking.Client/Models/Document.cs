using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocTracking.Client.Models
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Type { get; set; }

        public string Status { get; set; } = "In Progress";

        public int? CurrentOfficeId { get; set; }

        [ForeignKey("CurrentOfficeId")]
        public Office? CurrentOffice { get; set; }

        public int? NextOfficeId { get; set; }

        [ForeignKey("NextOfficeId")]
        public Office? NextOffice { get; set; }

        public int? CurrentUnitId { get; set; }

        [ForeignKey("CurrentUnitId")]
        public Unit? CurrentUnit { get; set; }

        public int? NextUnitId { get; set; }

        [ForeignKey("NextUnitId")]
        public Unit? NextUnit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActionDate { get; set; }

        public int? CreatorId { get; set; }

        [ForeignKey("CreatorId")]
        public AppUser? Creator { get; set; }
    }
}
