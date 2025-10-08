using System.Windows;

namespace FairyKey.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            Version.Text = $"Version {AppInfo.Version}";
            githubLink.NavigateUri = new System.Uri(AppInfo.GitHub);
            websiteLink.NavigateUri = new System.Uri(AppInfo.Website);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}