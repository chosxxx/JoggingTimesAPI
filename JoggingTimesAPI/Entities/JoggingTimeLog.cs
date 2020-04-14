using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JoggingTimesAPI.Entities
{
    public class JoggingTimeLog
    {
        [Key]
        public int JoggingTimeLogId { get; set; }
        public string UserName { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
        public double DistanceMetres { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool Active { get; set; }

        public User User { get; set; }
    }
}
