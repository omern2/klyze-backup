using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly UserService      _userService;
        private readonly HenrikApiService _henrikApi;
        private GoogleAuthService _googleAuth;
        private CancellationTokenSource _googleLoginCts;

        // ─── Aşama 1: Google Giriş ─────────────────────────────────────────────
        [ObservableProperty] private bool _googleStepVisible = true;
        [ObservableProperty] private bool _riiotStepVisible = false;
        [ObservableProperty] private bool _isGoogleLoading = false;
        [ObservableProperty] private string _googleEmail = "";

        // ─── Aşama 2: Riot ID ──────────────────────────────────────────────────
        [ObservableProperty] private string _oyuncuAdi    = "";
        [ObservableProperty] private string _tag          = "";
        [ObservableProperty] private string _bolge        = "eu";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool   _isLoading    = false;

        // ─── Genel ─────────────────────────────────────────────────────────────
        [ObservableProperty] private string _authMessage = "";

        /// <summary>Giriş başarılı olunca tetiklenir.</summary>
        public event Action GirisBasarili;

        public IRelayCommand GirisCommand { get; }
        public IRelayCommand GoogleLoginCommand { get; }
        public IRelayCommand BackToGoogleCommand { get; }

        public LoginViewModel(UserService userService, HenrikApiService henrikApi)
        {
            _userService  = userService;
            _henrikApi    = henrikApi;
            GirisCommand  = new AsyncRelayCommand(GirisYapAsync);
            GoogleLoginCommand = new AsyncRelayCommand(GoogleLoginAsync);
            BackToGoogleCommand = new RelayCommand(() => ResetToGoogleStep());
        }

        /// <summary>
        /// GoogleAuthService'yi inject et (App.xaml.cs'den çağrılır).
        /// </summary>
        public void SetGoogleAuth(GoogleAuthService googleAuth)
        {
            _googleAuth = googleAuth;
        }

        // ─── Aşama 1: Google ile Giriş ─────────────────────────────────────────

        private async Task GoogleLoginAsync()
        {
            if (_googleAuth == null)
            {
                AuthMessage = "Google giriş servisi kullanılamıyor.";
                return;
            }

            IsGoogleLoading = true;
            AuthMessage = "Tarayıcıda giriş yapın...";
            _googleLoginCts = new CancellationTokenSource();

            try
            {
                var result = await _googleAuth.SignInAsync(_googleLoginCts.Token);

                if (!result.Success)
                {
                    AuthMessage = result.Error ?? "Google giriş başarısız.";
                    return;
                }

                if (!string.IsNullOrEmpty(result.FirebaseIdToken))
                {
                    // Firebase auth state'ini Google hesabına göre güncelle
                    App.Firebase?.SetAuthState(result.FirebaseIdToken, result.FirebaseUid);

                    GoogleEmail = result.Email ?? "";
                    AuthMessage = "";

                    // Firebase'de mevcut profil var mı kontrol et
                    var existingProfile = await CheckExistingProfileAsync();

                    if (existingProfile != null)
                    {
                        // Ban kontrolü
                        if (existingProfile.IsBanned)
                        {
                            AuthMessage = "Hesabınız engellenmiştir. Lütfen destek ekibiyle iletişime geçin.";
                            return;
                        }

                        // Kayıtlı profil var → direkt giriş yap
                        _userService.SaveProfile(existingProfile);
                        GirisBasarili?.Invoke();
                    }
                    else
                    {
                        // İlk kez giriş → Riot ID adımına geç
                        GoogleStepVisible = false;
                        RiiotStepVisible = true;
                    }
                }
                else
                {
                    AuthMessage = "Firebase giriş başarısız. Lütfen tekrar deneyin.";
                }
            }
            catch (Exception ex)
            {
                AuthMessage = $"Bağlantı hatası: {ex.Message}";
            }
            finally
            {
                IsGoogleLoading = false;
            }
        }

        /// <summary>
        /// Firebase'de bu UID ile kayıtlı profil var mı kontrol et.
        /// </summary>
        private async Task<Models.UserProfile> CheckExistingProfileAsync()
        {
            try
            {
                if (App.Firebase == null) return null;
                var uid = App.Firebase.LocalId;
                if (string.IsNullOrEmpty(uid)) return null;

                var json = await App.Firebase.GetUserProfileAsync(uid);
                if (string.IsNullOrEmpty(json)) return null;

                var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                var ad = data["oyuncuAdi"]?.ToString();
                var tag = data["tag"]?.ToString();

                if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(tag))
                    return null;

                return new Models.UserProfile
                {
                    OyuncuAdi = ad,
                    Tag = tag,
                    Bolge = data["bolge"]?.ToString() ?? "eu",
                    Elo = data["elo"]?.ToObject<int>() ?? 0,
                    CurrentTier = data["currentTier"]?.ToObject<int>() ?? 0,
                    RutbePuani = data["rutbePuani"]?.ToObject<int>() ?? 0,
                    CardSmallUrl = data["cardSmallUrl"]?.ToString() ?? "",
                    Email = data["email"]?.ToString() ?? GoogleEmail,
                    GoogleUid = uid,
                    IsBanned = data["banned"]?.ToObject<bool>() ?? false,
                    SonGuncelleme = data["sonGuncelleme"]?.ToObject<long>() ?? 0
                };
            }
            catch { return null; }
        }

        // ─── Aşama 2: Riot ID ile Giriş ────────────────────────────────────────

        private async Task GirisYapAsync()
        {
            ErrorMessage = "";

            var ad  = OyuncuAdi?.Trim();
            var tag = Tag?.Trim().TrimStart('#');

            // Kullanıcı full Riot ID'yi (# dahil) tek alana yazmışsa otomatik ayır
            if (string.IsNullOrEmpty(tag) && ad?.Contains('#') == true)
            {
                var idx = ad.IndexOf('#');
                tag = ad.Substring(idx + 1).Trim();
                ad  = ad.Substring(0, idx).Trim();
            }

            if (string.IsNullOrEmpty(ad))
            {
                ErrorMessage = "Lütfen Riot kullanıcı adını girin.";
                return;
            }
            if (string.IsNullOrEmpty(tag))
            {
                ErrorMessage = "Lütfen tag'i girin (örnek: TR1).";
                return;
            }

            IsLoading = true;

            try
            {
                var girisTask = _userService.GirisVeGuncelleAsync(ad, tag, _henrikApi);
                var timeoutTask = Task.Delay(25000);

                var tamamlanan = await Task.WhenAny(girisTask, timeoutTask);

                if (tamamlanan == timeoutTask)
                {
                    ErrorMessage = "Sunucu yanıt vermiyor. Lütfen internet bağlantınızı kontrol edip tekrar deneyin.";
                    return;
                }

                var (basarili, hata, profil) = await girisTask;

                if (basarili)
                {
                    if (profil == null || string.IsNullOrEmpty(profil.OyuncuAdi))
                    {
                        ErrorMessage = "Hesap bulunamadı. Kullanıcı adı ve tag'i kontrol edin.";
                        return;
                    }

                    if (string.IsNullOrEmpty(profil.Bolge))
                    {
                        profil.Bolge = Bolge;
                        _userService.SaveProfile(profil);
                    }

                    // Firebase'e de kaydet
                    await SaveToFirebaseAsync(profil);

                    GirisBasarili?.Invoke();
                    return;
                }

                ErrorMessage = hata ?? "Hesap bulunamadı. Kullanıcı adı ve tag'i kontrol edin.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Bağlantı hatası: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveToFirebaseAsync(Models.UserProfile profil)
        {
            try
            {
                if (App.Firebase == null) return;
                var uid = App.Firebase.LocalId;
                if (string.IsNullOrEmpty(uid)) return;

                profil.GoogleUid = uid;
                profil.Email = GoogleEmail;

                var firebaseProfile = new
                {
                    oyuncuAdi = profil.OyuncuAdi,
                    tag = profil.Tag,
                    bolge = profil.Bolge,
                    elo = profil.Elo,
                    currentTier = profil.CurrentTier,
                    rutbePuani = profil.RutbePuani,
                    cardSmallUrl = profil.CardSmallUrl,
                    email = GoogleEmail,
                    googleUid = uid,
                    sonGuncelleme = profil.SonGuncelleme
                };

                await App.Firebase.SaveUserProfileAsync(uid, firebaseProfile);
            }
            catch { }
        }

        /// <summary>
        /// Dışarıdan çağrılır — Google adımına geri dön.
        /// </summary>
        public void ResetToGoogleStep()
        {
            GoogleStepVisible = true;
            RiiotStepVisible = false;
            AuthMessage = "";
            OyuncuAdi = "";
            Tag = "";
            ErrorMessage = "";
        }
    }
}
