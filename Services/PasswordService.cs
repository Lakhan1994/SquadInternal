using Microsoft.AspNetCore.Identity;
using SquadInternal.Models;
using System;

namespace SquadInternal.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<User> _hasher = new();

        public string Hash(User user, string password)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty");

            return _hasher.HashPassword(user, password);
        }

        public bool Verify(User user, string password)
        {
            if (user == null)
                return false;

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                return false;

            if (string.IsNullOrWhiteSpace(password))
                return false;

            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                password
            );

            return result == PasswordVerificationResult.Success;
        }
    }
}
