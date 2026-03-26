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

        public string? ReceivingSchedule { get; set; }
        public ICollection<Unit>? Units { get; set; }

        public int WorkerCount { get; set; }

    }
}
