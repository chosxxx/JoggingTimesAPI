using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class JoggingTimeLogUpdateModel
    {
        [Required]
        public int JoggingTimeLogId { get; set; }
        [Required]
        public double DistanceMetres { get; set; }
    }
}
