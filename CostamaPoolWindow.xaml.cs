using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CostamaEditor.Data;
using CostamaEditor.Models;
using CostamaEditor.Services;

namespace CostamaEditor;

public partial class CostamaPoolWindow : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<ItemRecord> _items;
    private readonly ItemRecord _mainItem;
    private readonly GrfArchiveCatalog _grfCatalog;
    private string _searchText = string.Empty;
    private string _maxValueText = CostamaUseScriptGenerator.DefaultRandomRange.ToString(CultureInfo.InvariantCulture);
    private string _validationText = LocalizationService.Get("Pool.Validation.Empty");
    private Brush _validationBrush = Brushes.SlateGray;
    private string _scriptPreview = LocalizationService.Get("Pool.Status.EmptyPreview");
    private string _statusText = LocalizationService.Get("Pool.Status.Initial");
    private ItemRecord? _selectedCandidate;
    private RewardEntry? _selectedReward;
    private SourceFileEntry? _selectedTarget;
    private bool _poolIsValid;
    private bool _existingUseScriptEditable = true;

    public ObservableCollection<SourceFileEntry> TargetChoices { get; } = new();
    public ObservableCollection<ItemRecord> Candidates { get; } = new();
    public ObservableCollection<RewardEntry> Rewards { get; } = new();
    public ICollectionView CandidatesView { get; }
    public string MainItemSummary => LocalizationService.Get("Pool.MainSummary", _mainItem.Name, _mainItem.Id, System.IO.Path.GetFileName(_mainItem.DefinitionSource));
    public bool WasSaved { get; private set; }
    public string MaxValueText
    {
        get => _maxValueText;
        set
        {
            if (_maxValueText == value) return;
            _maxValueText = value;
            OnPropertyChanged();
            UpdateValidation();
        }
    }
    public string TotalWeightText
    {
        get
        {
            var total = Rewards.Sum(reward => TryParseWeight(reward.WeightText, out var weight) ? weight : 0);
            var max = TryParseMaxValue(MaxValueText, out var maxValue)
                ? maxValue.ToString(CultureInfo.InvariantCulture)
                : "—";
            return LocalizationService.Get("Pool.TotalWeight", total.ToString(CultureInfo.InvariantCulture), max);
        }
    }

    public ItemRecord? SelectedCandidate
    {
        get => _selectedCandidate;
        set { _selectedCandidate = value; OnPropertyChanged(); }
    }

    public RewardEntry? SelectedReward
    {
        get => _selectedReward;
        set { _selectedReward = value; OnPropertyChanged(); }
    }

    public SourceFileEntry? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (ReferenceEquals(_selectedTarget, value)) return;
            _selectedTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApply));
        }
    }

    public string CandidateCountText => LocalizationService.Get("Pool.Candidates.Count", CandidatesView.Cast<object>().Count(), Candidates.Count);
    public string ValidationText
    {
        get => _validationText;
        private set { _validationText = value; OnPropertyChanged(); }
    }

    public Brush ValidationBrush
    {
        get => _validationBrush;
        private set { _validationBrush = value; OnPropertyChanged(); }
    }

    public string ScriptPreview
    {
        get => _scriptPreview;
        private set { _scriptPreview = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public bool CanApply => _poolIsValid && SelectedTarget is not null && _existingUseScriptEditable;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CostamaPoolWindow(
        IReadOnlyList<SourceFileEntry> targetChoices,
        IReadOnlyList<ItemRecord> items,
        ItemRecord mainItem,
        GrfArchiveCatalog grfCatalog)
    {
        InitializeComponent();
        _items = items;
        _mainItem = mainItem;
        _grfCatalog = grfCatalog;
        foreach (var source in targetChoices)
            TargetChoices.Add(source);
        foreach (var item in items)
            Candidates.Add(item);
        CandidatesView = CollectionViewSource.GetDefaultView(Candidates);
        CandidatesView.Filter = FilterCandidate;
        DataContext = this;
        SelectedTarget = TargetChoices.FirstOrDefault(source => PathsEqual(source.Path, mainItem.DefinitionSource));
        LoadExistingRewards();
        UpdateValidation();
    }

    private void LoadExistingRewards()
    {
        var useScript = _mainItem.Scripts.FirstOrDefault() ?? string.Empty;
        if (!CostamaUseScriptGenerator.TryParse(useScript, out var randomRange, out var configuredRewards))
        {
            _existingUseScriptEditable = false;
            StatusText = LocalizationService.Get("Pool.Status.InvalidScript");
            OnPropertyChanged(nameof(CanApply));
            return;
        }

        MaxValueText = randomRange.ToString(CultureInfo.InvariantCulture);
        foreach (var configuredReward in configuredRewards)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == configuredReward.ItemId) ?? CreateMissingRewardItem(configuredReward.ItemId);
            var reward = new RewardEntry(
                item,
                configuredReward.Weight.ToString(CultureInfo.InvariantCulture),
                configuredReward.Quantity.ToString(CultureInfo.InvariantCulture));
            reward.PropertyChanged += Reward_PropertyChanged;
            Rewards.Add(reward);
            SelectedReward = reward;
            _ = LoadRewardIconAsync(reward);
        }

        if (configuredRewards.Count > 0)
            StatusText = LocalizationService.Get("Pool.Status.LoadedExisting", configuredRewards.Count);
        else
            StatusText = LocalizationService.Get("Pool.Status.EmptyPool");
        CandidatesView.Refresh();
        OnPropertyChanged(nameof(CandidateCountText));
    }

    private ItemRecord CreateMissingRewardItem(int itemId) => new()
    {
        Id = itemId,
        Name = LocalizationService.Get("Pool.MissingItem", itemId),
        Category = string.Empty,
        JapaneseName = string.Empty,
        Type = string.Empty,
        RawFields = Array.Empty<string>(),
        Scripts = Array.Empty<string>(),
        DefinitionSource = _mainItem.DefinitionSource,
        DefinitionHistory = Array.Empty<string>(),
    };

    private void CandidateSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = CandidateSearchBox.Text.Trim();
        CandidatesView.Refresh();
        OnPropertyChanged(nameof(CandidateCountText));
    }

    private bool FilterCandidate(object candidate)
    {
        if (candidate is not ItemRecord item || item.Id == _mainItem.Id || Rewards.Any(reward => reward.Item.Id == item.Id))
            return false;
        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        var terms = _searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fields = new[]
        {
            item.Id.ToString(CultureInfo.InvariantCulture), item.Name, item.JapaneseName, item.Category, item.ResourceName ?? string.Empty,
        };
        return terms.Any(term => fields.Any(field => field.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private void AddReward_Click(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidateGrid.SelectedItems.OfType<ItemRecord>().ToList();
        if (selectedCandidates.Count == 0 && SelectedCandidate is not null)
            selectedCandidates.Add(SelectedCandidate);
        selectedCandidates = selectedCandidates
            .DistinctBy(item => item.Id)
            .Where(item => Rewards.All(reward => reward.Item.Id != item.Id))
            .ToList();
        if (selectedCandidates.Count == 0)
        {
            StatusText = LocalizationService.Get("Pool.Status.SelectCandidate");
            return;
        }
        if (!TryParseMaxValue(MaxValueText, out var maxValue))
        {
            StatusText = LocalizationService.Get("Pool.Status.InvalidMaxValue");
            return;
        }
        if (Rewards.Count == 0 && maxValue < selectedCandidates.Count)
        {
            StatusText = LocalizationService.Get("Pool.Status.MaxBelowItemCount");
            return;
        }
        if (Rewards.Count + selectedCandidates.Count > 100)
        {
            StatusText = LocalizationService.Get("Pool.Status.MaxItems");
            return;
        }

        var isEmptyPool = Rewards.Count == 0;
        var evenWeight = maxValue / selectedCandidates.Count;
        var remainder = maxValue % selectedCandidates.Count;
        var initialWeight = Math.Max(1, maxValue / 100);
        RewardEntry? lastAddedReward = null;
        for (var index = 0; index < selectedCandidates.Count; index++)
        {
            var weight = isEmptyPool
                ? evenWeight + (index < remainder ? 1 : 0)
                : initialWeight;
            var reward = new RewardEntry(selectedCandidates[index], weight.ToString(CultureInfo.InvariantCulture), "1");
            reward.PropertyChanged += Reward_PropertyChanged;
            Rewards.Add(reward);
            lastAddedReward = reward;
            _ = LoadRewardIconAsync(reward);
        }

        SelectedReward = lastAddedReward;
        SelectedCandidate = null;
        CandidatesView.Refresh();
        OnPropertyChanged(nameof(CandidateCountText));
        StatusText = LocalizationService.Get("Pool.Status.Added", selectedCandidates.Count);
        UpdateValidation();
    }

    private void RemoveReward_Click(object sender, RoutedEventArgs e)
    {
        RewardGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        RewardGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        var selectedRewards = RewardGrid.SelectedItems.OfType<RewardEntry>().ToList();
        if (selectedRewards.Count == 0 && SelectedReward is not null)
            selectedRewards.Add(SelectedReward);
        if (selectedRewards.Count == 0)
            return;

        foreach (var reward in selectedRewards)
        {
            reward.PropertyChanged -= Reward_PropertyChanged;
            Rewards.Remove(reward);
        }
        if (Rewards.Count > 0 && TryParseMaxValue(MaxValueText, out var maxValue))
            RebalanceWeights(Rewards, maxValue);
        SelectedReward = null;
        CandidatesView.Refresh();
        OnPropertyChanged(nameof(CandidateCountText));
        StatusText = Rewards.Count > 0
            ? LocalizationService.Get("Pool.Status.Removed", selectedRewards.Count)
            : LocalizationService.Get("Pool.Status.EmptyAfterRemoval");
        UpdateValidation();
    }

    private void RewardGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
            return;
        if (RewardGrid.SelectedItems.Count == 0 && SelectedReward is null)
            return;

        e.Handled = true;
        RemoveReward_Click(sender, new RoutedEventArgs());
    }

    private static void RebalanceWeights(IReadOnlyList<RewardEntry> rewards, int maxValue)
    {
        if (rewards.Count == 0 || rewards.Count > 100 || maxValue < rewards.Count)
            return;

        var weights = rewards
            .Select(reward => TryParseWeight(reward.WeightText, out var value) ? value : 0)
            .ToArray();
        if (weights.Any(weight => weight == 0))
            Array.Fill(weights, 1);

        var distributable = maxValue - rewards.Count;
        var totalWeight = weights.Sum(weight => (long)weight);
        var redistributedWeights = new int[rewards.Count];
        var remainders = new List<(int Index, long Remainder)>();
        long distributed = 0;
        for (var index = 0; index < rewards.Count; index++)
        {
            var weighted = (long)distributable * weights[index];
            var share = weighted / totalWeight;
            redistributedWeights[index] = (int)share + 1;
            distributed += share;
            remainders.Add((index, weighted % totalWeight));
        }

        foreach (var remainder in remainders.OrderByDescending(value => value.Remainder).ThenBy(value => value.Index).Take((int)(distributable - distributed)))
            redistributedWeights[remainder.Index]++;

        for (var index = 0; index < rewards.Count; index++)
            rewards[index].WeightText = redistributedWeights[index].ToString(CultureInfo.InvariantCulture);
    }

    private void Reward_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RewardEntry.WeightText) or nameof(RewardEntry.QuantityText))
            UpdateValidation();
    }

    private void Target_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(CanApply));

    private void UpdateValidation()
    {
        var maxIsValid = TryParseMaxValue(MaxValueText, out var maxValue);
        var invalidWeight = Rewards.Any(reward => !TryParseWeight(reward.WeightText, out var weight) || (maxIsValid && weight > maxValue));
        var invalidQuantity = Rewards.Any(reward => !TryParseQuantity(reward.QuantityText, out _));
        var total = Rewards.Sum(reward => TryParseWeight(reward.WeightText, out var value) ? value : 0);
        var formattedTotal = total.ToString(CultureInfo.InvariantCulture);
        var formattedMax = maxIsValid ? maxValue.ToString(CultureInfo.InvariantCulture) : "—";
        _poolIsValid = maxIsValid && !invalidWeight && !invalidQuantity &&
                       (Rewards.Count == 0 ? total == 0 : total == maxValue);

        if (!maxIsValid)
        {
            ValidationText = LocalizationService.Get("Pool.Validation.BadMaxValue");
            ValidationBrush = Brushes.LightCoral;
            ScriptPreview = LocalizationService.Get("Pool.Preview.Fix");
        }
        else if (Rewards.Count == 0)
        {
            ValidationText = LocalizationService.Get("Pool.Validation.Empty");
            ValidationBrush = Brushes.MediumAquamarine;
            ScriptPreview = LocalizationService.Get("Pool.Preview.EmptyPool");
        }
        else if (invalidWeight || invalidQuantity)
        {
            ValidationText = invalidWeight
                ? LocalizationService.Get("Pool.Validation.BadWeight", formattedTotal, formattedMax)
                : LocalizationService.Get("Pool.Validation.BadQuantity", formattedTotal, formattedMax);
            ValidationBrush = Brushes.LightCoral;
            ScriptPreview = LocalizationService.Get("Pool.Preview.Fix");
        }
        else if (total != maxValue)
        {
            ValidationText = LocalizationService.Get("Pool.Validation.NotMax", formattedTotal, formattedMax);
            ValidationBrush = Brushes.Khaki;
            ScriptPreview = LocalizationService.Get("Pool.Preview.NeedMax");
        }
        else
        {
            ValidationText = LocalizationService.Get("Pool.Validation.Valid", formattedTotal, formattedMax, Rewards.Count);
            ValidationBrush = Brushes.MediumAquamarine;
            ScriptPreview = GenerateUseScript();
        }

        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(TotalWeightText));
    }

    private async Task LoadRewardIconAsync(RewardEntry reward)
    {
        if (string.IsNullOrWhiteSpace(reward.Item.ResourceName))
        {
            reward.IconStatus = LocalizationService.Get("Pool.Icon.NoResource");
            return;
        }

        var lookup = await Task.Run(() => _grfCatalog.FindIcon(reward.Item.ResourceName));
        if (!Rewards.Contains(reward))
            return;
        if (lookup.Bytes is null)
        {
            reward.IconStatus = lookup.Error ?? LocalizationService.Get("Pool.Icon.NotFound");
            return;
        }

        try
        {
            reward.Icon = ItemIconImageFactory.Create(lookup.Bytes);
            reward.IconStatus = $"{lookup.ArchivePath}\\{lookup.EntryPath}";
        }
        catch (Exception ex)
        {
            reward.IconStatus = LocalizationService.Get("Pool.Icon.DisplayFailed", ex.Message);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        RewardGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        RewardGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        UpdateValidation();
        if (!CanApply)
        {
            StatusText = SelectedTarget is null ? LocalizationService.Get("Pool.Status.TargetRequired") : ValidationText;
            return;
        }

        var editor = new ItemEditorWindow(TargetChoices.ToList(), _items, _mainItem) { Owner = this };
        editor.SetTargetPath(SelectedTarget!.Path);
        editor.SetUseScript(GenerateUseScript());
        if (editor.ShowDialog() == true && editor.WasSaved)
        {
            WasSaved = true;
            DialogResult = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private string GenerateUseScript()
    {
        if (Rewards.Count == 0)
            return string.Empty;

        var values = Rewards.Select(reward =>
        {
            _ = TryParseWeight(reward.WeightText, out var weight);
            _ = TryParseQuantity(reward.QuantityText, out var quantity);
            return new CostamaReward(reward.Item.Id, weight, quantity);
        }).ToList();
        _ = TryParseMaxValue(MaxValueText, out var maxValue);
        return CostamaUseScriptGenerator.Generate(values, maxValue);
    }

    private static bool TryParseWeight(string value, out int weight) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out weight) &&
        weight is >= 1 and <= CostamaUseScriptGenerator.MaximumRandomRange;

    private static bool TryParseMaxValue(string value, out int maxValue) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maxValue) &&
        maxValue is >= 1 and <= CostamaUseScriptGenerator.MaximumRandomRange;

    private static bool TryParseQuantity(string value, out int quantity) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0;

    private static bool PathsEqual(string left, string right) =>
        string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class RewardEntry(ItemRecord item, string weight, string quantity) : INotifyPropertyChanged
    {
        private string _weightText = weight;
        private string _quantityText = quantity;
        private ImageSource? _icon;
        private string _iconStatus = LocalizationService.Get("Pool.Icon.NotLoaded");

        public ItemRecord Item { get; } = item;
        public string WeightText
        {
            get => _weightText;
            set { _weightText = value; OnPropertyChanged(); }
        }
        public string QuantityText
        {
            get => _quantityText;
            set { _quantityText = value; OnPropertyChanged(); }
        }
        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }
        public string IconStatus
        {
            get => _iconStatus;
            set { _iconStatus = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
