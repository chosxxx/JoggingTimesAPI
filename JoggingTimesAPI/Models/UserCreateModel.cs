using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class UserCreateModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Username { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string EmailAddress { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; }
        [Required(AllowEmptyStrings = false)]
        public UserRole Role { get; set; }
    }
}
