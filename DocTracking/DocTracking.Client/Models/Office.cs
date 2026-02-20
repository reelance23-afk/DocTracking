using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocTracking.Client.Models
{
    public class Office
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? WorkerEmail { get; set; }
    }
}
