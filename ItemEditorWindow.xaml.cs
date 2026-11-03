using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using CostamaEditor.Data;
using CostamaEditor.Models;
using CostamaEditor.Services;

namespace CostamaEditor;

public partial class ItemEditorWindow : Window, INotifyPropertyChanged
{
    private static readonly string[] FieldLabelKeys =
    {
        "Editor.Field.Id", "Editor.Field.Name", "Editor.Field.Jname", "Editor.Field.Type", "Editor.Field.Price", "Editor.Field.Sell",
        "Editor.Field.Weight", "Editor.Field.ATK", "Editor.Field.DEF", "Editor.Field.Range", "Editor.Field.Slot", "Editor.Field.Job",
        "Editor.Field.Gender", "Editor.Field.Loc", "Editor.Field.WLv", "Editor.Field.ELv", "Editor.Field.View", "Editor.Field.Refine",
    };

    private static readonly string[] ScriptLabels = { "UseScript", "EquipScript", "UnequipScript" };
    private readonly ItemDbWriter _writer = new();
    private readonly IReadOnlyList<ItemRecord> _catalogItems;
    private readonly ItemRecord? _editingItem;
    private ItemDbWritePreview? _preview;
    private bool _isOrderBlocked;
    private bool _requiresConflictAcknowledgement;
    private string _previewText = LocalizationService.Get("Editor.Preview.Initial");
    private string _previewStatus = LocalizationService.Get("Editor.Preview.Unpreviewed");
    private string _statusText = LocalizationService.Get("Editor.Status.Initial");
    private SourceFileEntry? _selectedTarget;

    public ObservableCollection<SourceFileEntry> TargetChoices { get; } = new();
    public ObservableCollection<EditorValue> Fields { get; } = new();
    public ObservableCollection<EditorValue> Scripts { get; } = new();
    public string WindowTitle => _editingItem is null
        ? LocalizationService.Get("Editor.Title.New")
        : LocalizationService.Get("Editor.Title.Edit", _editingItem.Name, _editingItem.Id);
    public bool IsEditing => _editingItem is not null;
    public bool WasSaved { get; private set; }

    public SourceFileEntry? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (ReferenceEquals(_selectedTarget, value)) return;
            _selectedTarget = value;
            OnPropertyChanged();
            InvalidatePreview();
        }
    }

    public string PreviewText
    {
        get => _previewText;
        private set { _previewText = value; OnPropertyChanged(); }
    }

    public string PreviewStatus
    {
        get => _previewStatus;
        private set { _previewStatus = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ItemEditorWindow(
        IReadOnlyList<SourceFileEntry> targetChoices,
        IReadOnlyList<ItemRecord> catalogItems,
        ItemRecord? editingItem)
    {
        InitializeComponent();
        DataContext = this;
        _catalogItems = catalogItems;
        _editingItem = editingItem;
        OnPropertyChanged(nameof(WindowTitle));

        foreach (var source in targetChoices)
            TargetChoices.Add(source);
        var rawFields = editingItem?.RawFields.ToArray() ?? Enumerable.Repeat(string.Empty, ItemDbLineParser.FieldCount).ToArray();
        if (editingItem is null)
            rawFields[3] = string.Empty;
        for (var index = 0; index < FieldLabelKeys.Length; index++)
        {
            var field = new EditorValue(LocalizationService.Get(FieldLabelKeys[index]), rawFields[index], readOnly: index == 0 && editingItem is not null);
            field.PropertyChanged += EditorValue_PropertyChanged;
            Fields.Add(field);
        }

        for (var index = 0; index < ScriptLabels.Length; index++)
        {
            var value = editingItem is null
                ? "{}"
                : index < editingItem.Scripts.Length ? editingItem.Scripts[index] : string.Empty;
            var script = new EditorValue(ScriptLabels[index], value);
            script.PropertyChanged += EditorValue_PropertyChanged;
            Scripts.Add(script);
        }

        if (editingItem is not null)
            SelectedTarget = TargetChoices.FirstOrDefault(source => PathsEqual(source.Path, editingItem.DefinitionSource));
        SaveButton.IsEnabled = false;
    }

    public void SetTargetPath(string path) =>
        SelectedTarget = TargetChoices.FirstOrDefault(source => PathsEqual(source.Path, path));

    public void SetUseScript(string script) => Scripts[0].Value = script;

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SelectedTarget is null)
                throw new InvalidDataException(LocalizationService.Get("Editor.Status.TargetRequired"));

            var fields = Fields.Select(field => field.Value).ToArray();
            var scripts = Scripts.Select(script => script.Value).ToArray();
            var id = ParseId(fields[0]);
            var existing = _catalogItems.FirstOrDefault(item => item.Id == id);
            var editingSameDefinition = _editingItem is not null &&
                                        id == _editingItem.Id &&
                                        PathsEqual(SelectedTarget.Path, _editingItem.DefinitionSource);
            _requiresConflictAcknowledgement = existing is not null && !editingSameDefinition;
            _isOrderBlocked = false;

            if (existing is not null && !editingSameDefinition)
            {
                var sourceIndex = FindSourceIndex(existing.DefinitionSource);
                var targetIndex = FindSourceIndex(SelectedTarget.Path);
                if (sourceIndex >= 0 && targetIndex >= 0 && targetIndex < sourceIndex)
                    _isOrderBlocked = true;
            }

            var preview = _writer.Prepare(SelectedTarget.Path, fields, scripts);
            _preview = preview;
            ConflictCheckBox.IsChecked = false;
            ConflictCheckBox.Visibility = _requiresConflictAcknowledgement ? Visibility.Visible : Visibility.Collapsed;
            var operation = LocalizationService.Get(preview.IsAdd ? "Editor.Preview.Add" : "Editor.Preview.Replace");
            var original = preview.OriginalLine is null ? LocalizationService.Get("Editor.Preview.NoOriginal") : preview.OriginalLine;
            PreviewText = $"{LocalizationService.Get("Editor.Preview.Target", preview.TargetPath)}{Environment.NewLine}{LocalizationService.Get("Editor.Preview.Operation", operation)}{Environment.NewLine}{LocalizationService.Get("Editor.Preview.Id", preview.ItemId)}{Environment.NewLine}{Environment.NewLine}{LocalizationService.Get("Editor.Preview.Original")}{Environment.NewLine}{original}{Environment.NewLine}{Environment.NewLine}{LocalizationService.Get("Editor.Preview.New")}{Environment.NewLine}{preview.NewLine}";

            if (_isOrderBlocked)
            {
                PreviewStatus = LocalizationService.Get("Editor.Status.OrderBlocked");
                StatusText = LocalizationService.Get("Editor.Status.OrderBlockedDetail");
            }
            else if (_requiresConflictAcknowledgement)
            {
                PreviewStatus = LocalizationService.Get("Editor.Preview.Conflict", id, Path.GetFileName(existing!.DefinitionSource));
                StatusText = LocalizationService.Get("Editor.Status.ConflictDetail");
            }
            else
            {
                PreviewStatus = LocalizationService.Get("Editor.Status.PreviewReady");
                StatusText = LocalizationService.Get("Editor.Status.PreviewReadyDetail");
            }
            UpdateSaveEnabled();
        }
        catch (Exception ex)
        {
            _preview = null;
            PreviewStatus = LocalizationService.Get("Editor.Status.PreviewFailed");
            PreviewText = ex.Message;
            StatusText = ex.Message;
            SaveButton.IsEnabled = false;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_preview is null || !CanSave())
            return;
        try
        {
            var backupPath = _writer.Save(_preview);
            WasSaved = true;
            StatusText = LocalizationService.Get("Editor.Status.Saved", backupPath);
            MessageBox.Show(this, LocalizationService.Get("Editor.Message.Saved", backupPath), LocalizationService.Get("Editor.Message.SavedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            PreviewStatus = LocalizationService.Get("Editor.Status.SaveFailed");
            StatusText = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e) => InvalidatePreview();
    private void EditorValue_TextChanged(object sender, TextChangedEventArgs e) => InvalidatePreview();
    private void EditorValue_PropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidatePreview();
    private void ConflictCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateSaveEnabled();

    private void InvalidatePreview()
    {
        if (_preview is null)
            return;
        _preview = null;
        PreviewStatus = LocalizationService.Get("Editor.Status.FieldsChanged");
        SaveButton.IsEnabled = false;
    }

    private void UpdateSaveEnabled() => SaveButton.IsEnabled = CanSave();

    private bool CanSave() => _preview is not null && !_isOrderBlocked &&
                              (!_requiresConflictAcknowledgement || ConflictCheckBox.IsChecked == true);

    private int FindSourceIndex(string path)
    {
        for (var index = 0; index < TargetChoices.Count; index++)
        {
            if (PathsEqual(TargetChoices[index].Path, path))
                return index;
        }
        return -1;
    }

    private static int ParseId(string value)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new InvalidDataException(LocalizationService.Get("Writer.InvalidId"));
        return id;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class EditorValue(string label, string value, bool readOnly = false) : INotifyPropertyChanged
    {
        private string _value = value;
        public string Label { get; } = label;
        public bool IsReadOnly { get; } = readOnly;
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
