using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using CostamaEditor.Services;

namespace CostamaEditor.Models;

public sealed class SourceFileEntry(string path) : INotifyPropertyChanged
{
    private string _status = LocalizationService.Get("Source.NotLoaded");
    private string? _detail;

    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBrush)); }
    }

    public Brush StatusBrush => ContainsAny("未載入", "Not loaded", "未読み込み")
        ? Brushes.SlateGray
        : ContainsAny("失敗", "錯誤", "不支援", "找不到", "缺少", "failed", "error", "unsupported", "not found", "missing", "エラー", "未対応", "見つかりません")
        ? Brushes.LightCoral
        : ContainsAny("警告", "略過", "warning", "skipped", "スキップ")
            ? Brushes.Khaki
            : ContainsAny("載入", "開啟", "找到", "loaded", "opened", "found", "読み込", "開き", "見つけ")
                ? Brushes.MediumAquamarine
                : Brushes.SlateGray;

    private bool ContainsAny(params string[] values) =>
        values.Any(value => _status.Contains(value, StringComparison.OrdinalIgnoreCase));

    public string? Detail
    {
        get => _detail;
        set { _detail = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
