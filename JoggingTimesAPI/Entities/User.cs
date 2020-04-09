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
        [Key]
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        [NotMapped]
        public string NewPassword { set; get; }
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

        public void SetHashedPassword() => SetHashedPassword(NewPassword);
        public void SetHashedPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password cannot be empty.");

            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                PasswordHashKey = hmac.Key;
                PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }

            NewPassword = null;
        }
    }
}
