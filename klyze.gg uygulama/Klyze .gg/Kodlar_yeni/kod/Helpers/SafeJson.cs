using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace ValorantAutoClicker.Helpers
{
    public static class SafeJson
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            TypeNameHandling = TypeNameHandling.None
        };

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public static string Serialize(object value, Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(value, formatting, Settings);
        }
    }
}
