using Microsoft.Win32;
using System.Windows;
using VortexModLists.Services;

namespace VortexModLists
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            LocalizationService.InitializeCulture();
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ThemeService.ApplyTheme(this);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            var window = new MainWindow();
            window.SourceInitialized += (_, _) => ThemeService.ApplyWindowChromeTheme(window);
            window.Show();
            ThemeService.ApplyWindowChromeTheme(window);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            base.OnExit(e);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
            {
                ThemeService.ApplyTheme(this);

                foreach (Window window in Current.Windows)
                {
                    ThemeService.ApplyWindowChromeTheme(window);
                }
            }
        }
    }
}
