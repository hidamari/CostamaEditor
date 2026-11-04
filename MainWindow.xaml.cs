using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CostamaEditor.Data;
using CostamaEditor.Models;
using CostamaEditor.Services;
using Microsoft.Win32;

namespace CostamaEditor;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ItemDbReader _itemDbReader = new();
    private readonly ItemInfoReader _itemInfoReader = new();
    private AppSettings _settings;
    private GrfArchiveCatalog? _grfCatalog;
    private ItemRecord? _selectedItem;
    private ImageSource? _previewImage;
    private string _previewMessage = string.Empty;
    private string _statusText = string.Empty;
    private string _resultCountText = string.Empty;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _catalogCurrent;
    private bool _initializingLanguage;
    private string? _settingsSaveError;
    private IReadOnlyList<SourceLoadResult> _itemDbLoadResults = Array.Empty<SourceLoadResult>();
    private IReadOnlyList<SourceLoadResult> _itemInfoLoadResults = Array.Empty<SourceLoadResult>();
    private IReadOnlyList<SourceLoadResult> _grfLoadResults = Array.Empty<SourceLoadResult>();

    public ObservableCollection<SourceFileEntry> ItemDbSources { get; } = new();
    public ObservableCollection<SourceFileEntry> ItemInfoSources { get; } = new();
    public ObservableCollection<SourceFileEntry> GrfSources { get; } = new();
    public ObservableCollection<ItemRecord> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    public ItemRecord? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            if (EditItemButton is not null)
                EditItemButton.IsEnabled = value is not null && _catalogCurrent;
            if (CostamaButton is not null)
                CostamaButton.IsEnabled = value is not null && _catalogCurrent;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedItemHistoryText));
        }
    }

    public string SelectedItemHistoryText => SelectedItem is null
        ? string.Empty
        : SelectedItem.DefinitionHistory.Count == 0
            ? LocalizationService.Get("Main.HistoryNone")
            : LocalizationService.Get("Main.HistorySome", string.Join(
                LocalizationService.CurrentLanguage == "en" ? ", " : "、",
                SelectedItem.DefinitionHistory));

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            _previewImage = value;
            OnPropertyChanged();
            if (IconPlaceholder is not null)
                IconPlaceholder.Visibility = value is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public string PreviewMessage
    {
        get => _previewMessage;
        private set { _previewMessage = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public string ResultCountText
    {
        get => _resultCountText;
        private set { _resultCountText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FilterItem;
        DataContext = this;
        _settings = _settingsStore.Load();
        _initializingLanguage = true;
        LocalizationService.SetLanguage(_settings.Language);
        UpdateGridHeaders();
        _settings.Language = LocalizationService.CurrentLanguage;
        LanguageComboBox.SelectedValue = _settings.Language;
        StatusText = LocalizationService.Get("Main.Status.Ready");
        PreviewMessage = LocalizationService.Get("Main.Preview.Select");
        ResultCountText = LocalizationService.Get("Main.Results.NotLoaded");
        _initializingLanguage = false;
        FillSources(ItemDbSources, _settings.ItemDbSources);
        FillSources(ItemInfoSources, _settings.ItemInfoSources);
        FillSources(GrfSources, _settings.GrfSources);
        UpdateGrfEditorPathDisplay();
        Closed += (_, _) => _grfCatalog?.Dispose();
    }

    private async void Reload_Click(object sender, RoutedEventArgs e) => await ReloadSourcesAsync();

    private async void ChooseGrfEditorDll_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = $"{LocalizationService.Get("Main.Grf.LibraryFilter")} (GRF.dll)|GRF.dll|{LocalizationService.Get("Main.FileFilter.All")} (*.*)|*.*",
            InitialDirectory = File.Exists(_settings.GrfEditorDllPath)
                ? Path.GetDirectoryName(_settings.GrfEditorDllPath) ?? string.Empty
                : string.Empty,
        };
        if (dialog.ShowDialog(this) != true) return;

        if (!string.Equals(Path.GetFileName(dialog.FileName), "GRF.dll", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, LocalizationService.Get("Main.Grf.InvalidDll"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var utilitiesPath = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "Utilities.dll");
        if (!File.Exists(utilitiesPath))
        {
            MessageBox.Show(this, LocalizationService.Get("Main.Grf.UtilitiesMissing"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.GrfEditorDllPath = Path.GetFullPath(dialog.FileName);
        UpdateGrfEditorPathDisplay();
        await ReloadSourcesAsync();
    }

    private async void UseAutomaticGrfEditorPath_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.GrfEditorDllPath = null;
        UpdateGrfEditorPathDisplay();
        await ReloadSourcesAsync();
    }

    private async void Language_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_initializingLanguage || LanguageComboBox.SelectedValue is not string language)
            return;

        LocalizationService.SetLanguage(language);
        UpdateGridHeaders();
        UpdateGrfEditorPathDisplay();
        _settings.Language = LocalizationService.CurrentLanguage;
        SaveSettings();
        SetSourceStatuses(ItemDbSources, _itemDbLoadResults);
        SetSourceStatuses(ItemInfoSources, _itemInfoLoadResults);
        SetSourceStatuses(GrfSources, _grfLoadResults);
        UpdateResultCountText();
        OnPropertyChanged(nameof(SelectedItemHistoryText));
        StatusText = _settingsSaveError is null
            ? LocalizationService.Get("Main.Status.LanguageChanged")
            : LocalizationService.Get("Main.Status.LanguageSaveFailed", _settingsSaveError);
        await UpdateItemPreviewAsync(SelectedItem);
    }

    private void UpdateGridHeaders()
    {
        ItemsGrid.Columns[0].Header = LocalizationService.Get("Grid.Id");
        ItemsGrid.Columns[1].Header = LocalizationService.Get("Grid.Name");
        ItemsGrid.Columns[2].Header = LocalizationService.Get("Grid.Category");
        ItemsGrid.Columns[3].Header = LocalizationService.Get("Grid.JapaneseName");
        ItemsGrid.Columns[4].Header = LocalizationService.Get("Grid.Type");
        ItemsGrid.Columns[5].Header = LocalizationService.Get("Grid.DefinitionSource");
        ItemsGrid.Columns[6].Header = LocalizationService.Get("Grid.ResourceName");
    }

    private void UpdateGrfEditorPathDisplay()
    {
        var configuredPath = _settings.GrfEditorDllPath;
        var detectedPath = GrfArchiveCatalog.ResolveLibraryPath(configuredPath);

        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            GrfEditorPathText.Text = LocalizationService.Get("Main.Grf.Path.Custom", configuredPath);
            GrfEditorPathText.ToolTip = configuredPath;
        }
        else if (!string.IsNullOrWhiteSpace(configuredPath) && detectedPath is not null)
        {
            GrfEditorPathText.Text = LocalizationService.Get("Main.Grf.Path.CustomMissingFallback", configuredPath, detectedPath);
            GrfEditorPathText.ToolTip = detectedPath;
        }
        else if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            GrfEditorPathText.Text = LocalizationService.Get("Main.Grf.Path.CustomMissing", configuredPath);
            GrfEditorPathText.ToolTip = configuredPath;
        }
        else if (detectedPath is not null)
        {
            GrfEditorPathText.Text = LocalizationService.Get("Main.Grf.Path.Auto", detectedPath);
            GrfEditorPathText.ToolTip = detectedPath;
        }
        else
        {
            GrfEditorPathText.Text = LocalizationService.Get("Main.Grf.Path.NotFound");
            GrfEditorPathText.ToolTip = null;
        }
    }

    private async Task ReloadSourcesAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        StatusText = LocalizationService.Get("Main.Status.Loading");
        ResultCountText = LocalizationService.Get("Main.Results.Loading");
        SaveSettings();
        var settingsSaveError = _settingsSaveError;

        try
        {
            var dbPaths = ItemDbSources.Select(source => source.Path).ToList();
            var infoPaths = ItemInfoSources.Select(source => source.Path).ToList();
            var grfPaths = GrfSources.Select(source => source.Path).ToList();
            var loaded = await Task.Run(() =>
            {
                var dbResult = _itemDbReader.Read(dbPaths);
                var infoResult = _itemInfoReader.Read(infoPaths);
                var grfCatalog = new GrfArchiveCatalog(_settings.GrfEditorDllPath);
                var grfResults = grfCatalog.Load(grfPaths);
                foreach (var item in dbResult.Items)
                {
                    if (infoResult.Items.TryGetValue(item.Id, out var info))
                    {
                        item.ResourceName = info.ResourceName;
                        item.ItemInfoSource = info.SourcePath;
                    }
                }
                return new LoadedSources(dbResult, infoResult, grfCatalog, grfResults);
            });

            var oldCatalog = _grfCatalog;
            _grfCatalog = loaded.GrfCatalog;
            oldCatalog?.Dispose();

            _itemDbLoadResults = loaded.ItemDbResult.Sources;
            _itemInfoLoadResults = loaded.ItemInfoResult.Sources;
            _grfLoadResults = loaded.GrfResults;
            SetSourceStatuses(ItemDbSources, _itemDbLoadResults);
            SetSourceStatuses(ItemInfoSources, _itemInfoLoadResults);
            SetSourceStatuses(GrfSources, _grfLoadResults);
            Items.Clear();
            foreach (var item in loaded.ItemDbResult.Items)
                Items.Add(item);

            SelectedItem = null;
            PreviewImage = null;
            PreviewMessage = LocalizationService.Get("Main.Preview.Select");
            ItemsView.Refresh();
            ResultCountText = LocalizationService.Get("Main.Results.Count", Items.Count);
            StatusText = settingsSaveError is null
                ? LocalizationService.Get("Main.Status.Loaded", loaded.ItemDbResult.Sources.Count, loaded.ItemInfoResult.Sources.Count, loaded.GrfResults.Count)
                : LocalizationService.Get("Main.Status.LoadedSaveFailed", loaded.ItemDbResult.Sources.Count, loaded.ItemInfoResult.Sources.Count, loaded.GrfResults.Count, settingsSaveError);
            _catalogCurrent = true;
            EditItemButton.IsEnabled = SelectedItem is not null;
            CostamaButton.IsEnabled = SelectedItem is not null;
            OnPropertyChanged(nameof(SelectedItem));
        }
        catch (Exception ex)
        {
            _catalogCurrent = false;
            EditItemButton.IsEnabled = false;
            CostamaButton.IsEnabled = false;
            StatusText = LocalizationService.Get("Main.Status.LoadFailed", ex.Message);
            ResultCountText = LocalizationService.Get("Main.Results.LoadFailed");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void NewItem_Click(object sender, RoutedEventArgs e) => await OpenItemEditorAsync(null);

    private async void EditItem_Click(object sender, RoutedEventArgs e) => await OpenItemEditorAsync(SelectedItem);

    private async void CostamaPool_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is null)
            return;
        if (!_catalogCurrent || _grfCatalog is null)
        {
            MessageBox.Show(this, LocalizationService.Get("Main.Dialog.Reload.Grf"), LocalizationService.Get("Main.Dialog.Reload.Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new CostamaPoolWindow(ItemDbSources.ToList(), Items.ToList(), SelectedItem, _grfCatalog) { Owner = this };
        if (editor.ShowDialog() == true && editor.WasSaved)
            await ReloadSourcesAsync();
    }

    private async Task OpenItemEditorAsync(ItemRecord? item)
    {
        if (_isLoading)
            return;
        if (!_catalogCurrent)
        {
            MessageBox.Show(this, LocalizationService.Get("Main.Dialog.Reload.Catalog"), LocalizationService.Get("Main.Dialog.Reload.Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (ItemDbSources.Count == 0)
        {
            MessageBox.Show(this, LocalizationService.Get("Main.Dialog.NoTarget"), LocalizationService.Get("Main.Dialog.NoTarget.Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new ItemEditorWindow(ItemDbSources.ToList(), Items.ToList(), item) { Owner = this };
        if (editor.ShowDialog() == true && editor.WasSaved)
            await ReloadSourcesAsync();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        ItemsView.Refresh();
        UpdateResultCountText();
    }

    private bool FilterItem(object item)
    {
        if (item is not ItemRecord record || string.IsNullOrEmpty(_searchText))
            return true;
        var term = _searchText;
        return record.Id.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
               record.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               record.JapaneseName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               record.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               (record.ResourceName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async void ItemGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var item = ItemsGrid.SelectedItem as ItemRecord;
        if (!ReferenceEquals(SelectedItem, item))
            SelectedItem = item;
        await UpdateItemPreviewAsync(item);
    }

    private async Task UpdateItemPreviewAsync(ItemRecord? item)
    {
        PreviewImage = null;
        if (item is null)
        {
            PreviewMessage = LocalizationService.Get("Main.Preview.Select");
            return;
        }

        if (string.IsNullOrWhiteSpace(item.ResourceName))
        {
            PreviewMessage = LocalizationService.Get("Main.Preview.NoResource");
            return;
        }

        var catalog = _grfCatalog;
        if (catalog is null)
        {
            PreviewMessage = LocalizationService.Get("Main.Preview.NoGrf", item.ResourceName, item.ItemInfoSource);
            return;
        }

        PreviewMessage = LocalizationService.Get("Main.Preview.Searching", item.ResourceName);
        var result = await Task.Run(() => catalog.FindIcon(item.ResourceName));
        if (!ReferenceEquals(item, SelectedItem))
            return;

        if (result.Bytes is null)
        {
            PreviewMessage = LocalizationService.Get("Main.Preview.IconError", item.ResourceName, item.ItemInfoSource, result.Error);
            return;
        }

        try
        {
            item.IconBytes = result.Bytes;
            item.IconSource = result.ArchivePath;
            item.IconEntryPath = result.EntryPath;
            PreviewImage = ItemIconImageFactory.Create(result.Bytes);
            PreviewMessage = LocalizationService.Get("Main.Preview.IconFound", item.ItemInfoSource, result.ArchivePath, result.EntryPath);
        }
        catch (Exception ex)
        {
            PreviewMessage = LocalizationService.Get("Main.Preview.DisplayFailed", ex.Message);
        }
    }

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var kind = (sender as System.Windows.Controls.Button)?.Tag?.ToString();
        var list = GetSourceList(kind);
        if (list is null) return;

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Filter = kind switch
            {
                "ItemDb" => $"{LocalizationService.Get("Main.FileFilter.ItemDb")} (*.txt)|*.txt|{LocalizationService.Get("Main.FileFilter.All")} (*.*)|*.*",
                "ItemInfo" => $"{LocalizationService.Get("Main.FileFilter.ItemInfo")} (*.lua;*.lub)|*.lua;*.lub|{LocalizationService.Get("Main.FileFilter.All")} (*.*)|*.*",
                _ => $"{LocalizationService.Get("Main.FileFilter.Grf")} (*.grf;*.gpf)|*.grf;*.gpf|{LocalizationService.Get("Main.FileFilter.All")} (*.*)|*.*",
            },
        };
        if (dialog.ShowDialog(this) != true) return;

        foreach (var path in dialog.FileNames)
        {
            if (list.Any(source => string.Equals(source.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(new SourceFileEntry(path));
        }
        OnSourcesChanged();
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var kind = (sender as System.Windows.Controls.Button)?.Tag?.ToString();
        var (list, selected) = GetListAndSelection(kind);
        if (list is null || selected is null) return;
        list.Remove(selected);
        OnSourcesChanged();
    }

    private void MoveSourceUp_Click(object sender, RoutedEventArgs e) => MoveSource(sender, -1);
    private void MoveSourceDown_Click(object sender, RoutedEventArgs e) => MoveSource(sender, 1);

    private void MoveSource(object sender, int delta)
    {
        if (_isLoading) return;
        var kind = (sender as System.Windows.Controls.Button)?.Tag?.ToString();
        var (list, selected) = GetListAndSelection(kind);
        if (list is null || selected is null) return;
        var oldIndex = list.IndexOf(selected);
        var newIndex = oldIndex + delta;
        if (newIndex < 0 || newIndex >= list.Count) return;
        list.Move(oldIndex, newIndex);
        SelectSource(kind, selected);
        OnSourcesChanged();
    }

    private void OnSourcesChanged()
    {
        _catalogCurrent = false;
        EditItemButton.IsEnabled = false;
        CostamaButton.IsEnabled = false;
        SaveSettings();
        StatusText = _settingsSaveError is null
            ? LocalizationService.Get("Main.Status.SourceChanged")
            : LocalizationService.Get("Main.Status.SourceSaveFailed", _settingsSaveError);
    }

    private void SaveSettings()
    {
        _settings.ItemDbSources = ItemDbSources.Select(source => source.Path).ToList();
        _settings.ItemInfoSources = ItemInfoSources.Select(source => source.Path).ToList();
        _settings.GrfSources = GrfSources.Select(source => source.Path).ToList();
        _settings.Language = LocalizationService.CurrentLanguage;
        try
        {
            _settingsStore.Save(_settings);
            _settingsSaveError = null;
        }
        catch (Exception ex)
        {
            _settingsSaveError = ex.Message;
        }
    }

    private ObservableCollection<SourceFileEntry>? GetSourceList(string? kind) => kind switch
    {
        "ItemDb" => ItemDbSources,
        "ItemInfo" => ItemInfoSources,
        "Grf" => GrfSources,
        _ => null,
    };

    private (ObservableCollection<SourceFileEntry>? List, SourceFileEntry? Selected) GetListAndSelection(string? kind) => kind switch
    {
        "ItemDb" => (ItemDbSources, ItemDbList.SelectedItem as SourceFileEntry),
        "ItemInfo" => (ItemInfoSources, ItemInfoList.SelectedItem as SourceFileEntry),
        "Grf" => (GrfSources, GrfList.SelectedItem as SourceFileEntry),
        _ => (null, null),
    };

    private void SelectSource(string? kind, SourceFileEntry selected)
    {
        switch (kind)
        {
            case "ItemDb": ItemDbList.SelectedItem = selected; break;
            case "ItemInfo": ItemInfoList.SelectedItem = selected; break;
            case "Grf": GrfList.SelectedItem = selected; break;
        }
    }

    private static void FillSources(ObservableCollection<SourceFileEntry> destination, IEnumerable<string> paths)
    {
        foreach (var path in paths)
            destination.Add(new SourceFileEntry(path));
    }

    private static void SetSourceStatuses(ObservableCollection<SourceFileEntry> destination, IReadOnlyList<SourceLoadResult> results)
    {
        var statuses = results.GroupBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        foreach (var source in destination)
        {
            if (statuses.TryGetValue(source.Path, out var result))
            {
                source.Status = result.StatusKey is null
                    ? result.Status
                    : LocalizationService.Get(result.StatusKey, result.StatusArguments ?? Array.Empty<object?>());
                source.Detail = result.DetailKey is null
                    ? result.Detail
                    : LocalizationService.Get(result.DetailKey, result.DetailArguments ?? Array.Empty<object?>());
            }
            else
            {
                source.Status = LocalizationService.Get("Source.NotLoaded");
                source.Detail = null;
            }
        }
    }

    private void UpdateResultCountText()
    {
        if (!_catalogCurrent)
        {
            ResultCountText = LocalizationService.Get("Main.Results.NotLoaded");
            return;
        }

        var visibleCount = ItemsView.Cast<object>().Count();
        ResultCountText = string.IsNullOrWhiteSpace(_searchText)
            ? LocalizationService.Get("Main.Results.Count", Items.Count)
            : LocalizationService.Get("Main.Results.Visible", visibleCount, Items.Count);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private sealed record LoadedSources(
        ItemDbReadResult ItemDbResult,
        ItemInfoReadResult ItemInfoResult,
        GrfArchiveCatalog GrfCatalog,
        IReadOnlyList<SourceLoadResult> GrfResults);
}
