using System.Windows.Controls;
using System.Windows.Input;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class LoginPage : UserControl
    {
        private LoginViewModel VM => DataContext as LoginViewModel;

        public LoginPage()
        {
            InitializeComponent();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && VM?.GirisCommand?.CanExecute(null) == true)
                VM.GirisCommand.Execute(null);
        }
    }
}
