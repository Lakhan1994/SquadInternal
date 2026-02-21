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

        // ✅ SAFE VERIFY (supports Identity + custom + plain text)
        public bool Verify(User user, string password)
        {
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return false;

            // 1️⃣ Try ASP.NET Identity format
            var identityResult =
                _identityHasher.VerifyHashedPassword(user, user.PasswordHash, password);

            if (identityResult == PasswordVerificationResult.Success)
                return true;

            // 2️⃣ Try custom salt.hash format
            if (user.PasswordHash.Contains("."))
                return VerifyPassword(password, user.PasswordHash);

            // 3️⃣ Fallback plain text
            return user.PasswordHash == password;
        }

        // 🔐 Custom Hash (for future passwords)
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

        // 🔍 Verify Custom Hash
        private bool VerifyPassword(string enteredPassword, string storedHash)
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
    }
}