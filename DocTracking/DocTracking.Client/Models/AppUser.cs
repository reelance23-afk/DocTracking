using System.ComponentModel.DataAnnotations.Schema;

namespace DocTracking.Client.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        public string? Email { get; set; }

        public string? Role { get; set; }

        public int? UnitId { get; set; }

        [ForeignKey("UnitId")]
        public Unit? Unit { get; set; }

    }
}
