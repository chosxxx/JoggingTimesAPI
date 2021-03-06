﻿using System.ComponentModel.DataAnnotations;

namespace JoggingTimesAPI.Models
{
    public class UserAuthenticateModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Username { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; }
    }
}
