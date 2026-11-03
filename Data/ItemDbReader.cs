using System.IO;
using System.Text;
using CostamaEditor.Models;
using CostamaEditor.Services;

namespace CostamaEditor.Data;

public sealed class ItemDbReader
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "acc", "ammo", "armor", "box", "card", "cos", "ench", "head", "heal", "item",
        "pet", "robe", "shadow", "shoes", "shield", "throw", "unknown", "use", "weapon",
    };

    public ItemDbReadResult Read(IEnumerable<string> orderedPaths)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var latest = new Dictionary<int, ItemRecord>();
        var sourceResults = new List<SourceLoadResult>();

        foreach (var path in orderedPaths)
        {
            if (!File.Exists(path))
            {
                sourceResults.Add(new SourceLoadResult(path, LocalizationService.Get("Source.FileNotFound"), StatusKey: "Source.FileNotFound"));
                continue;
            }

            var parsed = 0;
            var invalid = 0;
            try
            {
                using var reader = new StreamReader(path, Encoding.GetEncoding(932), detectEncodingFromByteOrderMarks: true);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    if (!ItemDbLineParser.TryParse(line, out var fields, out var scripts) ||
                        !int.TryParse(fields[0].Trim(), out var id) || id <= 0)
                    {
                        invalid++;
                        continue;
                    }

                    latest.TryGetValue(id, out var previous);
                    var category = fields[1].Trim();
                    var isCategory = Categories.Contains(category);
                    var name = isCategory
                        ? string.IsNullOrEmpty(previous?.Name) ? $"AURIGA_DB_{id}" : previous.Name
                        : category;

                    var history = previous is not null
                        ? previous.DefinitionHistory.Append(previous.DefinitionSource).ToList()
                        : new List<string>();

                    latest[id] = new ItemRecord
                    {
                        Id = id,
                        Name = name,
                        Category = isCategory ? category : string.Empty,
                        JapaneseName = fields[2].Trim(),
                        Type = fields[3].Trim(),
                        RawFields = fields,
                        Scripts = scripts,
                        DefinitionSource = path,
                        DefinitionHistory = history,
                    };
                    parsed++;
                }

                var statusKey = invalid == 0 ? "Source.ItemDb.Loaded" : "Source.ItemDb.LoadedSkipped";
                var statusArguments = invalid == 0 ? new object?[] { parsed } : new object?[] { parsed, invalid };
                sourceResults.Add(new SourceLoadResult(
                    path,
                    LocalizationService.Get(statusKey, statusArguments),
                    invalid == 0 ? null : LocalizationService.Get("Source.ItemDb.InvalidRows", invalid),
                    statusKey,
                    statusArguments,
                    invalid == 0 ? null : "Source.ItemDb.InvalidRows",
                    invalid == 0 ? null : new object?[] { invalid }));
            }
            catch (Exception ex)
            {
                sourceResults.Add(new SourceLoadResult(path, LocalizationService.Get("Source.ReadFailed"), ex.Message, "Source.ReadFailed"));
            }
        }

        return new ItemDbReadResult(latest.Values.OrderBy(item => item.Id).ToList(), sourceResults);
    }
}

public sealed record ItemDbReadResult(IReadOnlyList<ItemRecord> Items, IReadOnlyList<SourceLoadResult> Sources);
