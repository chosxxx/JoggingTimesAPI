using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class UserUpdateModel
    {
        [Required]
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
    }
}
