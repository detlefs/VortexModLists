using System.Reflection;
using System.Windows;

namespace VortexModLists
{
    public partial class InfoDialog : Window
    {
        public InfoDialog()
        {
            InitializeComponent();
            InitializeInfo();
        }

        private void InitializeInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var appName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                ?? assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
                ?? assembly.GetName().Name
                ?? "VortexModLists";

            var author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
            if (string.IsNullOrWhiteSpace(author))
            {
                author = "Unknown";
            }

            var version = GetDisplayVersion(assembly);
            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

            AppNameTextBlock.Text = appName;
            AuthorTextBlock.Text = $"Autor: {author}";
            VersionTextBlock.Text = $"Version: {version}";
            CopyrightTextBlock.Text = copyright;
        }

        private static string GetDisplayVersion(Assembly assembly)
        {
            var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (Version.TryParse(fileVersion, out var parsedFileVersion))
            {
                return $"{parsedFileVersion.Major}.{parsedFileVersion.Minor}.{parsedFileVersion.Build}.{parsedFileVersion.Revision}";
            }

            var assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion is not null)
            {
                return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}.{assemblyVersion.Revision}";
            }

            return "Unknown";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
