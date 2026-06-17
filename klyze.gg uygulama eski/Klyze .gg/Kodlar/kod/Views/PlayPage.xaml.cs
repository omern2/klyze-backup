using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class PlayPage : UserControl
    {
        private PlayViewModel VM => DataContext as PlayViewModel;

        public PlayPage()
        {
            InitializeComponent();
        }

        private void MacBulButton_Click(object sender, RoutedEventArgs e)
        {
            VM?.MacBulCommand?.Execute(null);
        }

        private void LobiOlusturButton_Click(object sender, RoutedEventArgs e)
        {
            VM?.LobiOlusturCommand?.Execute(null);
        }

        private void JoinCodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && VM?.LobiOlusturOnayCommand?.CanExecute(null) == true)
                VM.LobiOlusturOnayCommand.Execute(null);
        }

        private void KopyalaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string kod && !string.IsNullOrEmpty(kod))
                Clipboard.SetText(kod);
        }
    }
}
