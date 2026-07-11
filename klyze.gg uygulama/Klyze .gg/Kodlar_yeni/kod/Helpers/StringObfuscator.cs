using System;
using System.Text;

namespace ValorantAutoClicker.Helpers
{
    internal static class StringObfuscator
    {
        internal static string Decode(string base64, byte key)
        {
            var data = Convert.FromBase64String(base64);
            for (int i = 0; i < data.Length; i++)
                data[i] ^= key;
            return Encoding.UTF8.GetString(data);
        }
    }
}
