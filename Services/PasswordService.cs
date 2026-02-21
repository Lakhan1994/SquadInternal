using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using SquadInternal.Models;

namespace SquadInternal.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<User> _identityHasher =
            new PasswordHasher<User>();

        public bool Verify(User user, string password)
        {
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return false;

            // 1️⃣ Try Identity hash safely
            try
            {
                var result = _identityHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    password);

                if (result == PasswordVerificationResult.Success)
                    return true;
            }
            catch
            {
                // Ignore if not Identity format
            }

            // 2️⃣ Try Custom PBKDF2 (salt.hash format)
            if (user.PasswordHash.Contains("."))
            {
                if (VerifyCustom(password, user.PasswordHash))
                    return true;
            }

            // 3️⃣ Fallback → Plain text (for old data only)
            return user.PasswordHash == password;
        }

        private bool VerifyCustom(string enteredPassword, string storedHash)
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2)
                return false;

            byte[] salt = Convert.FromBase64String(parts[0]);

            string hashed = Convert.ToBase64String(
                KeyDerivation.Pbkdf2(
                    password: enteredPassword,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 32));

            return hashed == parts[1];
        }

        public string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            string hashed = Convert.ToBase64String(
                KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 32));

            return $"{Convert.ToBase64String(salt)}.{hashed}";
        }
    }
}