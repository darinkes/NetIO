using System.Windows;
using System.Windows.Input;

namespace NetIO
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Refresh_OnClick(object sender, ExecutedRoutedEventArgs e)
        {
            var viewmodel = DataContext as MainWindowViewModel;
            if (viewmodel == null)
                return;
            viewmodel.Refresh();
        }
    }
}
