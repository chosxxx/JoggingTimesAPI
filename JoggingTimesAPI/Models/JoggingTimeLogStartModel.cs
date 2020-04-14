using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class JoggingTimeLogStartModel
    {

        [Required]
        public double Latitude { get; set; }
        [Required]
        public double Longitude { get; set; }
    }
}
