using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Models
{
    public class UserRegisterModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string EmailAddress { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
