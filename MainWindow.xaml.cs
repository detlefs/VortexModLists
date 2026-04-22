using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Navigation;
using VortexModLists.Models;
using VortexModLists.Services;

namespace VortexModLists
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string LayoutFileName = "layout.json";

        private readonly VortexStateService _vortexStateService = new();
        private readonly ObservableCollection<ModRow> _rows = [];
        private IReadOnlyList<ModEntry> _allMods = [];
        private StatusFilter _currentStatusFilter = StatusFilter.All;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLocalizedText();
            InitializeExportScope();
            InitializeStatusHeaderFilter();

            ModsDataGrid.ItemsSource = _rows;
            StateFilePathTextBox.Text = _vortexStateService.GetDefaultStatePath();

            RestoreLayout();

            Closing += MainWindow_Closing;

            LoadMods();
        }

        private void InitializeLocalizedText()
        {
            Title = LocalizationService.Get("AppTitle");
            StateFileLabel.Text = LocalizationService.Get("StateFileLabel");
            BrowseButton.Content = LocalizationService.Get("BrowseButton");
            ReloadButton.Content = LocalizationService.Get("ReloadButton");
            GameLabel.Text = LocalizationService.Get("GameLabel");
            ExportScopeLabel.Text = LocalizationService.Get("ExportScopeLabel");
            ExportCsvButton.Content = LocalizationService.Get("ExportCsvButton");
            ExportMarkdownButton.Content = LocalizationService.Get("ExportMarkdownButton");
            ExportExcelButton.Content = LocalizationService.Get("ExportExcelButton");
            CopyCsvButton.Content = LocalizationService.Get("CopyCsvButton");
            CopyMarkdownButton.Content = LocalizationService.Get("CopyMarkdownButton");

            ModNameColumn.Header = LocalizationService.Get("ColumnModName");
            IdColumn.Header = LocalizationService.Get("ColumnId");
            VersionColumn.Header = LocalizationService.Get("ColumnVersion");
            HomepageColumn.Header = LocalizationService.Get("ColumnHomepage");
        }

        private void InitializeExportScope()
        {
            ExportScopeComboBox.ItemsSource = new[]
            {
                new ExportScopeItem(ExportScope.SelectedGame, LocalizationService.Get("SelectedGameScope")),
                new ExportScopeItem(ExportScope.AllGames, LocalizationService.Get("AllGamesScope"))
            };
            ExportScopeComboBox.DisplayMemberPath = nameof(ExportScopeItem.DisplayName);
            ExportScopeComboBox.SelectedValuePath = nameof(ExportScopeItem.Scope);
            ExportScopeComboBox.SelectedValue = ExportScope.SelectedGame;
        }

        private void InitializeStatusHeaderFilter()
        {
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = LocalizationService.Get("ColumnStatus"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var filterButton = new Button
            {
                Content = "▾",
                Width = 16,
                Height = 16,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = LocalizationService.Get("StatusFilterLabel")
            };
            filterButton.Click += StatusFilterButton_Click;

            Grid.SetColumn(headerText, 0);
            Grid.SetColumn(filterButton, 1);
            headerGrid.Children.Add(headerText);
            headerGrid.Children.Add(filterButton);

            StatusColumn.Header = headerGrid;
        }

        private void StatusFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement target)
            {
                return;
            }

            var contextMenu = BuildStatusFilterMenu();
            contextMenu.PlacementTarget = target;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private ContextMenu BuildStatusFilterMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateStatusMenuItem(StatusFilter.All, LocalizationService.Get("StatusFilterAll")));
            menu.Items.Add(CreateStatusMenuItem(StatusFilter.Active, LocalizationService.Get("StatusFilterActive")));
            menu.Items.Add(CreateStatusMenuItem(StatusFilter.Inactive, LocalizationService.Get("StatusFilterInactive")));
            return menu;
        }

        private MenuItem CreateStatusMenuItem(StatusFilter filter, string text)
        {
            var item = new MenuItem
            {
                Header = text,
                Tag = filter,
                IsCheckable = true,
                IsChecked = _currentStatusFilter == filter
            };
            item.Click += StatusFilterMenuItem_Click;
            return item;
        }

        private void StatusFilterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: StatusFilter selectedFilter })
            {
                _currentStatusFilter = selectedFilter;
                RefreshGrid();
            }
        }

        private void LoadMods()
        {
            try
            {
                var result = _vortexStateService.LoadModsWithSource(StateFilePathTextBox.Text);
                _allMods = result.Mods;

                if (!string.IsNullOrWhiteSpace(result.ResolvedSourcePath))
                {
                    StateFilePathTextBox.Text = result.ResolvedSourcePath;
                }

                var selectedGame = GameComboBox.SelectedItem as string;

                var games = _allMods
                    .Select(m => m.Game)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                GameComboBox.ItemsSource = games;

                if (games.Count > 0)
                {
                    GameComboBox.SelectedItem = games.Contains(selectedGame ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        ? selectedGame
                        : games[0];
                }
                else
                {
                    GameComboBox.SelectedItem = null;
                    _rows.Clear();
                    MessageBox.Show(LocalizationService.Get("NoDataMessage"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                RefreshGrid();
            }
            catch
            {
                MessageBox.Show(LocalizationService.Get("LoadError"), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshGrid()
        {
            _rows.Clear();

            foreach (var mod in GetFilteredSelectedGameMods())
            {
                _rows.Add(new ModRow
                {
                    Game = mod.Game,
                    ModName = mod.ModName,
                    Id = mod.Id,
                    Version = mod.Version,
                    Homepage = mod.Homepage,
                    LinkText = string.IsNullOrWhiteSpace(mod.Homepage) ? string.Empty : LocalizationService.Get("LinkText"),
                    Status = mod.IsActive ? LocalizationService.Get("StatusActive") : LocalizationService.Get("StatusInactive")
                });
            }
        }

        private IEnumerable<ModEntry> GetFilteredSelectedGameMods()
        {
            return ApplyStatusFilter(GetSelectedGameMods());
        }

        private IEnumerable<ModEntry> GetSelectedGameMods()
        {
            var game = GameComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(game))
            {
                return [];
            }

            return _allMods.Where(m => string.Equals(m.Game, game, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<ModEntry> GetExportMods()
        {
            var scope = ExportScopeComboBox.SelectedValue is ExportScope selectedScope
                ? selectedScope
                : ExportScope.SelectedGame;

            var scopedMods = scope == ExportScope.AllGames ? _allMods : GetSelectedGameMods();
            return ApplyStatusFilter(scopedMods);
        }

        private IEnumerable<ModEntry> ApplyStatusFilter(IEnumerable<ModEntry> mods)
        {
            return _currentStatusFilter switch
            {
                StatusFilter.Active => mods.Where(m => m.IsActive),
                StatusFilter.Inactive => mods.Where(m => !m.IsActive),
                _ => mods
            };
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = LocalizationService.Get("OpenStateFileTitle"),
                Filter = LocalizationService.Get("StateFileFilter"),
                CheckFileExists = true,
                FileName = Path.GetFileName(StateFilePathTextBox.Text)
            };

            var directory = Path.GetDirectoryName(StateFilePathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }

            if (dialog.ShowDialog(this) == true)
            {
                StateFilePathTextBox.Text = dialog.FileName;
                SaveCustomStatePath(dialog.FileName);
                LoadMods();
            }
        }

        private void SaveCustomStatePath(string path)
        {
            try
            {
                var state = ReadLayoutState() ?? new LayoutState();
                state.CustomStatePath = path;

                var folder = GetLayoutFolderPath();
                Directory.CreateDirectory(folder);
                var filePath = Path.Combine(folder, LayoutFileName);
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // intentionally ignored
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMods();
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InfoDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private void GameComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshGrid();
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWithDialog(
                LocalizationService.Get("SaveCsvTitle"),
                LocalizationService.Get("SaveCsvFilter"),
                "mods.csv",
                path => File.WriteAllText(path, ExportService.ToCsv(GetExportMods(), LocalizationService.Get("StatusActive"), LocalizationService.Get("StatusInactive"))));
        }

        private void ExportMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWithDialog(
                LocalizationService.Get("SaveMarkdownTitle"),
                LocalizationService.Get("SaveMarkdownFilter"),
                "mods.md",
                path => File.WriteAllText(path, ExportService.ToMarkdown(GetExportMods(), LocalizationService.Get("StatusActive"), LocalizationService.Get("StatusInactive"))));
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            SaveWithDialog(
                LocalizationService.Get("SaveExcelTitle"),
                LocalizationService.Get("SaveExcelFilter"),
                "mods.xlsx",
                path => ExportService.ToExcel(path, GetExportMods(), LocalizationService.Get("StatusActive"), LocalizationService.Get("StatusInactive")));
        }

        private void CopyCsvButton_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(ExportService.ToCsv(GetExportMods(), LocalizationService.Get("StatusActive"), LocalizationService.Get("StatusInactive")));
        }

        private void CopyMarkdownButton_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(ExportService.ToMarkdown(GetExportMods(), LocalizationService.Get("StatusActive"), LocalizationService.Get("StatusInactive")));
        }

        private void SaveWithDialog(string title, string filter, string defaultFileName, Action<string> writer)
        {
            if (!GetExportMods().Any())
            {
                MessageBox.Show(LocalizationService.Get("NoDataMessage"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                writer(dialog.FileName);
                MessageBox.Show(LocalizationService.Get("ExportSuccess"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(LocalizationService.Get("ExportError"), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyToClipboard(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show(LocalizationService.Get("NoDataMessage"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(content);
                MessageBox.Show(LocalizationService.Get("ClipboardCopied"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(LocalizationService.Get("ClipboardError"), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Homepage_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch
            {
                // intentionally ignored
            }

            e.Handled = true;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreLayout();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveLayout();
        }

        private void RestoreLayout()
        {
            var state = ReadLayoutState();
            if (state is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.CustomStatePath) && File.Exists(state.CustomStatePath))
            {
                StateFilePathTextBox.Text = state.CustomStatePath;
            }

            if (IsValidWindowBounds(state.Left, state.Top, state.Width, state.Height))
            {
                Left = state.Left;
                Top = state.Top;
                Width = state.Width;
                Height = state.Height;
            }

            if (state.ColumnWidths.Count > 0)
            {
                ApplyColumnWidth("modName", ModNameColumn, state.ColumnWidths);
                ApplyColumnWidth("id", IdColumn, state.ColumnWidths);
                ApplyColumnWidth("version", VersionColumn, state.ColumnWidths);
                ApplyColumnWidth("homepage", HomepageColumn, state.ColumnWidths);
                ApplyColumnWidth("status", StatusColumn, state.ColumnWidths);
            }

            if (state.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void SaveLayout()
        {
            try
            {
                var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
                var state = new LayoutState
                {
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    IsMaximized = WindowState == WindowState.Maximized,
                    CustomStatePath = ReadLayoutState()?.CustomStatePath,
                    ColumnWidths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["modName"] = ModNameColumn.ActualWidth,
                        ["id"] = IdColumn.ActualWidth,
                        ["version"] = VersionColumn.ActualWidth,
                        ["homepage"] = HomepageColumn.ActualWidth,
                        ["status"] = StatusColumn.ActualWidth
                    }
                };

                var folder = GetLayoutFolderPath();
                Directory.CreateDirectory(folder);
                var filePath = Path.Combine(folder, LayoutFileName);
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // intentionally ignored
            }
        }

        private LayoutState? ReadLayoutState()
        {
            try
            {
                var filePath = Path.Combine(GetLayoutFolderPath(), LayoutFileName);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<LayoutState>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string GetLayoutFolderPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "VortexModLists");
        }

        private static bool IsValidWindowBounds(double left, double top, double width, double height)
        {
            if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height))
            {
                return false;
            }

            if (width < 500 || height < 400)
            {
                return false;
            }

            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            var right = left + width;
            var bottom = top + height;

            return right >= virtualLeft + 80
                && bottom >= virtualTop + 80
                && left <= virtualRight - 80
                && top <= virtualBottom - 80;
        }

        private static void ApplyColumnWidth(string key, DataGridColumn column, IReadOnlyDictionary<string, double> widths)
        {
            if (!widths.TryGetValue(key, out var value))
            {
                return;
            }

            if (double.IsNaN(value) || double.IsInfinity(value) || value < 40)
            {
                return;
            }

            column.Width = new DataGridLength(value, DataGridLengthUnitType.Pixel);
        }

        private sealed class LayoutState
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsMaximized { get; set; }
            public string? CustomStatePath { get; set; }
            public Dictionary<string, double> ColumnWidths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed record ExportScopeItem(ExportScope Scope, string DisplayName);

        private enum StatusFilter
        {
            All,
            Active,
            Inactive
        }
    }
}