using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocTracking.Client.Models
{
    public class Unit
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        public int OfficeId { get; set; }

        [ForeignKey("OfficeId")]
        public Office? Office { get; set; }
        public ICollection<AppUser>? Users { get; set; }
    }
}
