using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ValorantAutoClicker.ViewModels;

namespace ValorantAutoClicker.Views
{
    public partial class PlayerAnalysisPage : UserControl
    {
        private PlayerAnalysisViewModel VM => DataContext as PlayerAnalysisViewModel;

        public PlayerAnalysisPage()
        {
            InitializeComponent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && VM?.SearchCommand?.CanExecute(null) == true)
                VM.SearchCommand.Execute(null);
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.SearchCommand?.CanExecute(null) == true)
                VM.SearchCommand.Execute(null);
        }

        private void RetryBtn_Click(object sender, RoutedEventArgs e)
        {
            VM?.RetryCommand?.Execute(null);
        }

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.LoadMoreMatchesCommand?.CanExecute(null) == true)
                VM.LoadMoreMatchesCommand.Execute(null);
        }

        private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is string riotId)
                VM?.SelectHistoryCommand?.Execute(riotId);
        }
    }
}
