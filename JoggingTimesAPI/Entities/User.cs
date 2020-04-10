using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JoggingTimesAPI
{
    public enum UserRole
    {
        User = 1,
        Manager = 2,
        Admin = 3
    }

    public class User
    {
        private string _newPassword;

        [Key]
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        [NotMapped]
        public string NewPassword { 
            set
            {
                _newPassword = value;
                if (!string.IsNullOrEmpty(value))
                    SetHashedPassword();
            }
            get
            {
                return _newPassword;
            }
        }
        protected byte[] PasswordHashKey { get; set; }
        protected byte[] PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public bool ValidatePassword(string password)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512(PasswordHashKey))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != PasswordHash[i]) return false;
                }
            }

            return true;
        }

        private void SetHashedPassword()
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                PasswordHashKey = hmac.Key;
                PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(_newPassword));
            }
        }
    }
}
