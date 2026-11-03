using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CostamaEditor.Models;
using CostamaEditor.Services;

namespace CostamaEditor.Data;

public sealed class ItemInfoReader
{
    private static readonly Regex EntryStart = new(@"\[\s*(?<id>\d+)\s*\]\s*=\s*\{", RegexOptions.Compiled);
    private static readonly Regex ResourceName = new(
        @"(?<![A-Za-z0-9_])identifiedResourceName\b\s*=\s*(?:""(?<double>(?:\\.|[^""\\])*)""|'(?<single>(?:\\.|[^'\\])*)'|\[(?<equals>=*)\[(?<long>.*?)\]\k<equals>\])",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public ItemInfoReadResult Read(IEnumerable<string> priorityPaths)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var items = new Dictionary<int, ItemInfoRecord>();
        var sources = new List<SourceLoadResult>();

        foreach (var path in priorityPaths)
        {
            if (!File.Exists(path))
            {
                sources.Add(new SourceLoadResult(path, LocalizationService.Get("Source.FileNotFound"), StatusKey: "Source.FileNotFound"));
                continue;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var startsWithLuaChunk = bytes.Length >= 4 && bytes[0] == 0x1B &&
                                         bytes[1] == (byte)'L' && bytes[2] == (byte)'u' && bytes[3] == (byte)'a';
                var text = File.ReadAllText(path);
                if (text.Contains('\0') || startsWithLuaChunk)
                {
                    sources.Add(new SourceLoadResult(
                        path,
                        LocalizationService.Get("Source.ItemInfo.Unsupported"),
                        LocalizationService.Get("Source.ItemInfo.UnsupportedDetail"),
                        "Source.ItemInfo.Unsupported",
                        DetailKey: "Source.ItemInfo.UnsupportedDetail"));
                    continue;
                }

                var found = 0;
                foreach (Match entry in EntryStart.Matches(text))
                {
                    var close = FindTableEnd(text, entry.Index + entry.Length - 1);
                    if (close < 0 || !int.TryParse(entry.Groups["id"].Value, out var id))
                        continue;

                    var body = text.AsSpan(entry.Index + entry.Length, close - entry.Index - entry.Length).ToString();
                    var resourceMatch = ResourceName.Match(body);
                    if (!resourceMatch.Success)
                        continue;

                    var encoded = resourceMatch.Groups["double"].Success
                        ? resourceMatch.Groups["double"].Value
                        : resourceMatch.Groups["single"].Success
                            ? resourceMatch.Groups["single"].Value
                            : resourceMatch.Groups["long"].Value;
                    var resourceName = DecodeLuaString(encoded);
                    if (resourceName.Length == 0)
                        continue;

                    if (!items.ContainsKey(id))
                        items.Add(id, new ItemInfoRecord(id, resourceName, path));
                    found++;
                }

                var statusKey = found == 0 ? "Source.ItemInfo.NotFound" : "Source.ItemInfo.Found";
                var statusArguments = found == 0 ? Array.Empty<object?>() : new object?[] { found };
                var detailKey = found == 0 ? "Source.ItemInfo.NotFoundDetail" : "Source.ItemInfo.FoundDetail";
                sources.Add(new SourceLoadResult(
                    path,
                    LocalizationService.Get(statusKey, statusArguments),
                    LocalizationService.Get(detailKey),
                    statusKey,
                    statusArguments,
                    detailKey));
            }
            catch (Exception ex)
            {
                sources.Add(new SourceLoadResult(path, LocalizationService.Get("Source.ReadFailed"), ex.Message, "Source.ReadFailed"));
            }
        }

        return new ItemInfoReadResult(items, sources);
    }

    private static int FindTableEnd(string text, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            var current = text[i];
            if (current == '"' || current == '\'')
            {
                var quote = current;
                for (i++; i < text.Length; i++)
                {
                    if (text[i] == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (text[i] == quote)
                        break;
                }
                continue;
            }

            if (current == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                i += 2;
                if (i < text.Length && TrySkipLongBracket(text, ref i))
                    continue;
                while (i < text.Length && text[i] is not '\r' and not '\n')
                    i++;
                continue;
            }

            if (current == '[' && TrySkipLongBracket(text, ref i))
                continue;

            if (current == '{')
                depth++;
            else if (current == '}' && --depth == 0)
                return i;
        }

        return -1;
    }

    private static bool TrySkipLongBracket(string text, ref int index)
    {
        if (index >= text.Length || text[index] != '[')
            return false;

        var cursor = index + 1;
        while (cursor < text.Length && text[cursor] == '=')
            cursor++;
        if (cursor >= text.Length || text[cursor] != '[')
            return false;

        var closing = "]" + new string('=', cursor - index - 1) + "]";
        var closeIndex = text.IndexOf(closing, cursor + 1, StringComparison.Ordinal);
        index = closeIndex < 0 ? text.Length - 1 : closeIndex + closing.Length - 1;
        return true;
    }

    private static string DecodeLuaString(string value)
    {
        var output = new StringBuilder(value.Length);
        var escapedBytes = new List<byte>();
        void FlushBytes()
        {
            if (escapedBytes.Count == 0)
                return;
            output.Append(Encoding.GetEncoding(949).GetString(escapedBytes.ToArray()));
            escapedBytes.Clear();
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                FlushBytes();
                output.Append(value[i]);
                continue;
            }

            var next = value[++i];
            if (char.IsDigit(next))
            {
                var digits = new StringBuilder().Append(next);
                while (digits.Length < 3 && i + 1 < value.Length && char.IsDigit(value[i + 1]))
                    digits.Append(value[++i]);
                if (byte.TryParse(digits.ToString(), out var byteValue))
                    escapedBytes.Add(byteValue);
                continue;
            }

            FlushBytes();
            output.Append(next switch
            {
                'a' => '\a', 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r',
                't' => '\t', 'v' => '\v', '\\' => '\\', '"' => '"', '\'' => '\'',
                _ => next,
            });
        }

        FlushBytes();
        return output.ToString();
    }
}

public sealed record ItemInfoReadResult(IReadOnlyDictionary<int, ItemInfoRecord> Items, IReadOnlyList<SourceLoadResult> Sources);
