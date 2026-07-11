using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ValorantAutoClicker.Services
{
    public static class SystemPromptLoader
    {
        private static List<(string dosyaAdi, string icerik)> _prompts;
        private static readonly object _kilit = new();

        public static List<(string dosyaAdi, string icerik)> TumPromptlariGetir()
        {
            if (_prompts != null)
                return _prompts;

            lock (_kilit)
            {
                if (_prompts != null)
                    return _prompts;

                _prompts = new List<(string, string)>();
                var assembly = Assembly.GetExecutingAssembly();
                var prefix = "ValorantAutoClicker.SystemPrompts.";

                var kaynaklar = assembly.GetManifestResourceNames()
                    .Where(r => r.StartsWith(prefix) && r.EndsWith(".txt"))
                    .OrderBy(r => r)
                    .ToList();

                foreach (var kaynak in kaynaklar)
                {
                    using var stream = assembly.GetManifestResourceStream(kaynak);
                    if (stream == null) continue;
                    using var reader = new StreamReader(stream);
                    var icerik = reader.ReadToEnd();
                    var dosyaAdi = kaynak.Substring(prefix.Length);
                    if (!string.IsNullOrWhiteSpace(icerik))
                        _prompts.Add((dosyaAdi, icerik.Trim()));
                }

                return _prompts;
            }
        }
    }
}
