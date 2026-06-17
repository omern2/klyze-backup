using System;
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

        [ObservableProperty] private string _oyuncuAdi    = "";
        [ObservableProperty] private string _tag          = "";
        [ObservableProperty] private string _bolge        = "eu";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool   _isLoading    = false;

        /// <summary>Giriş başarılı olunca tetiklenir.</summary>
        public event Action GirisBasarili;

        public IRelayCommand GirisCommand { get; }

        public LoginViewModel(UserService userService, HenrikApiService henrikApi)
        {
            _userService  = userService;
            _henrikApi    = henrikApi;
            GirisCommand  = new AsyncRelayCommand(GirisYapAsync);
        }

        private async Task GirisYapAsync()
        {
            ErrorMessage = "";

            var ad  = OyuncuAdi?.Trim();
            var tag = Tag?.Trim().TrimStart('#');

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
                var (basarili, hata, profil) = await _userService.GirisVeGuncelleAsync(ad, tag, _henrikApi);

                if (basarili)
                {
                    // Bölgeyi kullanıcının seçtiğiyle güncelle (API'den gelen bölge yoksa)
                    if (profil != null && string.IsNullOrEmpty(profil.Bolge))
                    {
                        profil.Bolge = Bolge;
                        _userService.SaveProfile(profil);
                    }
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
    }
}
