using CostamaEditor.Services;

namespace CostamaEditor.Models;

public sealed class ItemRecord
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string JapaneseName { get; init; }
    public required string Type { get; init; }
    public required string[] RawFields { get; init; }
    public required string[] Scripts { get; init; }
    public required string DefinitionSource { get; init; }
    public required IReadOnlyList<string> DefinitionHistory { get; init; }
    public string DefinitionHistoryText => DefinitionHistory.Count == 0
        ? LocalizationService.Get("Main.HistoryNone")
        : LocalizationService.Get("Main.HistorySome", string.Join(
            LocalizationService.CurrentLanguage == "en" ? ", " : "、",
            DefinitionHistory));

    public string? ResourceName { get; set; }
    public string? ItemInfoSource { get; set; }
    public string? IconSource { get; set; }
    public string? IconEntryPath { get; set; }
    public byte[]? IconBytes { get; set; }
}

public sealed record SourceLoadResult(
    string Path,
    string Status,
    string? Detail = null,
    string? StatusKey = null,
    object?[]? StatusArguments = null,
    string? DetailKey = null,
    object?[]? DetailArguments = null);

public sealed record ItemInfoRecord(int Id, string ResourceName, string SourcePath);
