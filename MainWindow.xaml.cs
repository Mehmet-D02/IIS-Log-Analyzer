using System.Windows;

namespace IISLogAnalyzer_WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.MainViewModel();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Open datetime pickers via button clicks
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            StartDateTimeButton.Click += (s, args) => StartDateTimePopup.IsOpen = true;
            EndDateTimeButton.Click += (s, args) => EndDateTimePopup.IsOpen = true;
        }

        private void StartDateTimeConfirm_Click(object sender, RoutedEventArgs e)
        {
            StartDateTimePopup.IsOpen = false;
        }

        private void EndDateTimeConfirm_Click(object sender, RoutedEventArgs e)
        {
            EndDateTimePopup.IsOpen = false;
        }

        // Auto-select all text when TextBox gets focus (for easier editing)
        private void TimeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.SelectAll();
            }
        }
    }
}