using System.Windows.Controls;
using System.Windows.Input;

namespace ValorantAutoClicker.Views
{
    public partial class KlyzeAiPage : UserControl
    {
        public KlyzeAiPage()
        {
            InitializeComponent();
        }

        private void GirisKutusu_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.KlyzeAiViewModel vm)
                {
                    vm.GonderCommand.Execute(null);
                }
            }
        }
    }
}
