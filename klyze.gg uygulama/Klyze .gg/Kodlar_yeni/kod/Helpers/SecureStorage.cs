using System;
using System.Security.Cryptography;
using System.Text;

namespace ValorantAutoClicker.Helpers
{
    public static class SecureStorage
    {
        private static readonly byte[] Entropy = { 0x4B, 0x6C, 0x79, 0x7A, 0x65, 0x53, 0x65, 0x63 };

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch { return ""; }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                var cipherBytes = Convert.FromBase64String(cipherText);
                var plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch { return ""; }
        }
    }
}
