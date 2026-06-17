using System;
using System.Threading.Tasks;

namespace ValorantAutoClicker.Services
{
    public static class ApiKeyProvider
    {
        public static string HenrikDevKey { get; private set; } = "";

        public static bool IsLoaded => !string.IsNullOrEmpty(HenrikDevKey);

        public static async Task LoadFromFirebaseAsync(FirebaseService firebase)
        {
            try
            {
                var config = await firebase.GetConfigAsync();
                if (config != null)
                {
                    HenrikDevKey = config.HenrikDevKey ?? "";
                }
            }
            catch
            {
                HenrikDevKey = "";
            }
        }

        public static void Reset()
        {
            HenrikDevKey = "";
        }
    }

    public class FirebaseConfig
    {
        public string HenrikDevKey { get; set; } = "";
    }
}
