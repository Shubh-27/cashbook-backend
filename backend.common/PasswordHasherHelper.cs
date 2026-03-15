using Microsoft.AspNetCore.Identity;

namespace backend.common
{
    public static class PasswordHasherHelper
    {
        private static readonly PasswordHasher<string> _passwordHasher = new();

        public static string HashPassword(string password, string? user = null)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        public static bool VerifyPassword(string hashedPassword, string password)
        {
            var result = _passwordHasher.VerifyHashedPassword(null, hashedPassword, password);

            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
