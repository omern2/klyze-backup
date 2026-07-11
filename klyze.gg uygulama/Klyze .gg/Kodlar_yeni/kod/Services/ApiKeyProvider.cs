using System;
using System.Threading.Tasks;
using ValorantAutoClicker.Helpers;

namespace ValorantAutoClicker.Services
{
    public static class ApiKeyProvider
    {
        private static readonly string FallbackKey = Helpers.StringObfuscator.Decode(
            "jYGAk+j186HxoaTypuim/aSg6PHx86HopPPw9uj89/LyoPWgpPKmp/Q=", 0xC5);

        private static string _henrikDevKey = "";
        public static string HenrikDevKey
        {
            get
            {
                if (string.IsNullOrEmpty(_henrikDevKey))
                    return FallbackKey;
                return _henrikDevKey;
            }
            private set => _henrikDevKey = value ?? "";
        }

        public static bool IsLoaded => !string.IsNullOrEmpty(HenrikDevKey);

        private static string _riotApiKey = "";
        public static string RiotApiKey
        {
            get => _riotApiKey;
            private set => _riotApiKey = value ?? "";
        }

        private static string _googleAiKey = "";
        public static string GoogleAiKey
        {
            get => _googleAiKey;
            private set => _googleAiKey = value ?? "";
        }

        private static string _groqAiKey = "";
        public static string GroqAiKey
        {
            get => _groqAiKey;
            private set => _groqAiKey = value ?? "";
        }

        private static string _groqAiKey2 = "";
        public static string GroqAiKey2
        {
            get
            {
                if (string.IsNullOrEmpty(_groqAiKey2))
                    return "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";
                return _groqAiKey2;
            }
            private set => _groqAiKey2 = value ?? "";
        }

        private static string _tavilyApiKey = "";
        public static string TavilyApiKey
        {
            get => _tavilyApiKey;
            private set => _tavilyApiKey = value ?? "";
        }

        private static string _openRouterAiKey = "";
        public static string OpenRouterAiKey
        {
            get => _openRouterAiKey;
            private set => _openRouterAiKey = value ?? "";
        }

        public static async Task LoadFromFirebaseAsync(FirebaseService firebase)
        {
            try
            {
                var config = await firebase.GetConfigAsync();
                if (config != null && !string.IsNullOrEmpty(config.HenrikDevKey))
                {
                    HenrikDevKey = config.HenrikDevKey;
                }
                if (config != null && !string.IsNullOrEmpty(config.RiotApiKey))
                {
                    RiotApiKey = config.RiotApiKey;
                }
                if (config != null && !string.IsNullOrEmpty(config.GoogleAiKey))
                {
                    GoogleAiKey = config.GoogleAiKey;
                }
                if (config != null && !string.IsNullOrEmpty(config.GroqAiKey))
                {
                    GroqAiKey = config.GroqAiKey;
                }
                if (config != null && !string.IsNullOrEmpty(config.GroqAiKey2))
                {
                    GroqAiKey2 = config.GroqAiKey2;
                }
                if (config != null && !string.IsNullOrEmpty(config.TavilyApiKey))
                {
                    TavilyApiKey = config.TavilyApiKey;
                }
                if (config != null && !string.IsNullOrEmpty(config.OpenRouterAiKey))
                {
                    OpenRouterAiKey = config.OpenRouterAiKey;
                }
            }
            catch { }
        }

        public static void Reset()
        {
            _henrikDevKey = "";
            _riotApiKey = "";
        }
    }

    public class FirebaseConfig
    {
        [Newtonsoft.Json.JsonProperty("henrikDevKey")]
        public string HenrikDevKey { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("riotApiKey")]
        public string RiotApiKey { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("googleAiKey")]
        public string GoogleAiKey { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("groqAiKey")]
        public string GroqAiKey { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("groqAiKey2")]
        public string GroqAiKey2 { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("tavilyApiKey")]
        public string TavilyApiKey { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("openRouterAiKey")]
        public string OpenRouterAiKey { get; set; } = "";
    }
}
