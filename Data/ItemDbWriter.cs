using System.IO;
using System.Security.Cryptography;
using System.Text;
using CostamaEditor.Services;

namespace CostamaEditor.Data;

public sealed class ItemDbWriter
{
    public ItemDbWritePreview Prepare(string targetPath, IReadOnlyList<string> fields, IReadOnlyList<string> scripts)
    {
        ArgumentNullException.ThrowIfNull(targetPath);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(scripts);
        if (fields.Count != ItemDbLineParser.FieldCount)
            throw new InvalidDataException(LocalizationService.Get("Writer.FieldCount", ItemDbLineParser.FieldCount));
        if (!int.TryParse(fields[0].Trim(), out var itemId) || itemId <= 0)
            throw new InvalidDataException(LocalizationService.Get("Writer.InvalidId"));

        ValidateFields(fields);
        var scriptTail = FormatScripts(scripts);
        var replacementLine = string.Join(',', fields) + "," + scriptTail;

        var document = TextFileDocument.Read(targetPath);
        var matches = document.Lines
            .Where(line => !line.Text.TrimStart().StartsWith("//", StringComparison.Ordinal) &&
                           ItemDbLineParser.TryParse(line.Text, out var parsed, out _) &&
                           int.TryParse(parsed[0].Trim(), out var parsedId) && parsedId == itemId)
            .ToList();
        if (matches.Count > 1)
            throw new InvalidDataException(LocalizationService.Get("Writer.DuplicateId", matches.Count, itemId));

        var encodedLine = document.Encoding.GetBytes(replacementLine);
        if (encodedLine.Length >= 4096)
            throw new InvalidDataException(LocalizationService.Get("Writer.LineTooLong"));

        var isAdd = matches.Count == 0;
        var oldLine = isAdd ? null : matches[0].Text;
        var updatedBytes = document.ReplaceLineOrAppend(matches.FirstOrDefault(), encodedLine);
        return new ItemDbWritePreview(
            Path.GetFullPath(targetPath), itemId, isAdd, oldLine, replacementLine,
            SHA256.HashData(document.Bytes), updatedBytes);
    }

    public string Save(ItemDbWritePreview preview)
    {
        var currentBytes = File.ReadAllBytes(preview.TargetPath);
        var currentHash = SHA256.HashData(currentBytes);
        if (!CryptographicOperations.FixedTimeEquals(currentHash, preview.OriginalHash))
            throw new IOException(LocalizationService.Get("Writer.PreviewChanged"));

        var backupPath = CreateBackupPath(preview.TargetPath);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(preview.TargetPath)!, $".{Path.GetFileName(preview.TargetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(preview.TargetPath, backupPath, overwrite: false);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(backupPath)), preview.OriginalHash))
                throw new IOException(LocalizationService.Get("Writer.BackupChanged"));
            File.WriteAllBytes(temporaryPath, preview.UpdatedBytes);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(preview.TargetPath)), preview.OriginalHash))
                throw new IOException(LocalizationService.Get("Writer.SaveChanged"));
            File.Move(temporaryPath, preview.TargetPath, overwrite: true);
            return backupPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void ValidateFields(IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i] is null)
                throw new InvalidDataException(LocalizationService.Get("Writer.NullField", i + 1));
            if (fields[i].Contains(',') || fields[i].Contains('\r') || fields[i].Contains('\n'))
                throw new InvalidDataException(LocalizationService.Get("Writer.InvalidField", i + 1));
        }
        if (string.IsNullOrWhiteSpace(fields[1]))
            throw new InvalidDataException(LocalizationService.Get("Writer.EmptyName"));
        if (string.IsNullOrWhiteSpace(fields[2]))
            throw new InvalidDataException(LocalizationService.Get("Writer.EmptyJname"));
        if (!int.TryParse(fields[3].Trim(), out _))
            throw new InvalidDataException(LocalizationService.Get("Writer.InvalidType"));
    }

    private static string FormatScripts(IReadOnlyList<string> scripts)
    {
        var selected = scripts.Take(3).Select(script => (script ?? string.Empty).Trim()).ToArray();
        var count = Array.FindLastIndex(selected, script => script.Length > 0) + 1;
        for (var i = 0; i < count; i++)
        {
            if (selected[i].Length == 0)
                selected[i] = "{}";
            if (selected[i].Contains('\r') || selected[i].Contains('\n'))
                throw new InvalidDataException(LocalizationService.Get("Writer.ScriptLine"));
            if (!selected[i].StartsWith('{') || !selected[i].EndsWith('}'))
                throw new InvalidDataException(LocalizationService.Get("Writer.ScriptBraces", i + 1));
        }
        return string.Join(',', selected.Take(count));
    }

    private static string CreateBackupPath(string targetPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture);
        var basePath = $"{targetPath}.bak.{timestamp}";
        var candidate = basePath;
        var suffix = 1;
        while (File.Exists(candidate))
            candidate = $"{basePath}.{suffix++}";
        return candidate;
    }
}

public sealed record ItemDbWritePreview(
    string TargetPath,
    int ItemId,
    bool IsAdd,
    string? OriginalLine,
    string NewLine,
    byte[] OriginalHash,
    byte[] UpdatedBytes);

internal sealed class TextFileDocument
{
    private TextFileDocument(byte[] bytes, Encoding encoding, int bomLength, IReadOnlyList<TextLine> lines, string newline, bool hasTrailingNewline)
    {
        Bytes = bytes;
        Encoding = encoding;
        BomLength = bomLength;
        Lines = lines;
        Newline = newline;
        HasTrailingNewline = hasTrailingNewline;
    }

    public byte[] Bytes { get; }
    public Encoding Encoding { get; }
    public int BomLength { get; }
    public IReadOnlyList<TextLine> Lines { get; }
    public string Newline { get; }
    public bool HasTrailingNewline { get; }

    public static TextFileDocument Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(LocalizationService.Get("Writer.TargetMissing"), path);

        var bytes = File.ReadAllBytes(path);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var (encoding, bomLength) = DetectEncoding(bytes);
        var isUtf16 = encoding.CodePage is 1200 or 1201;
        var lines = new List<TextLine>();
        var newlineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var lineStart = bomLength;
        var offset = bomLength;
        while (offset < bytes.Length)
        {
            var newlineLength = 0;
            string? newline = null;
            if (isUtf16)
            {
                if (TryReadUtf16Newline(bytes, offset, encoding.CodePage, out newline, out newlineLength))
                {
                    // Captured below as a complete line.
                }
                else
                {
                    offset += 2;
                    continue;
                }
            }
            else if (bytes[offset] == 0x0A)
            {
                newline = "\n";
                newlineLength = 1;
            }
            else if (bytes[offset] == 0x0D)
            {
                newline = offset + 1 < bytes.Length && bytes[offset + 1] == 0x0A ? "\r\n" : "\r";
                newlineLength = newline.Length;
            }
            else
            {
                offset++;
                continue;
            }

            var contentLength = offset - lineStart;
            var lineText = encoding.GetString(bytes, lineStart, contentLength);
            lines.Add(new TextLine(lineStart, contentLength, newlineLength, lineText));
            newlineCounts[newline!] = newlineCounts.GetValueOrDefault(newline!) + 1;
            offset += newlineLength;
            lineStart = offset;
        }

        var hasTrailingNewline = lineStart == bytes.Length && bytes.Length > bomLength;
        if (lineStart < bytes.Length)
        {
            var contentLength = bytes.Length - lineStart;
            lines.Add(new TextLine(lineStart, contentLength, 0, encoding.GetString(bytes, lineStart, contentLength)));
        }
        var newlineStyle = newlineCounts.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).FirstOrDefault() ?? "\r\n";
        return new TextFileDocument(bytes, encoding, bomLength, lines, newlineStyle, hasTrailingNewline);
    }

    public byte[] ReplaceLineOrAppend(TextLine? line, byte[] replacement)
    {
        using var output = new MemoryStream(Bytes.Length + replacement.Length + Encoding.GetByteCount(Newline));
        if (line is not null)
        {
            var suffixStart = line.Start + line.ContentLength;
            output.Write(Bytes, 0, line.Start);
            output.Write(replacement);
            output.Write(Bytes, suffixStart, Bytes.Length - suffixStart);
            return output.ToArray();
        }

        output.Write(Bytes);
        var newlineBytes = Encoding.GetBytes(Newline);
        if (Bytes.Length > BomLength && !HasTrailingNewline)
            output.Write(newlineBytes);
        output.Write(replacement);
        if (Bytes.Length == BomLength || HasTrailingNewline)
            output.Write(newlineBytes);
        return output.ToArray();
    }

    private static (Encoding Encoding, int BomLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            return (new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            return (new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true), 2);

        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            var text = strictUtf8.GetString(bytes);
            if (text.Any(character => character > 0x7F))
                return (strictUtf8, 0);
        }
        catch (DecoderFallbackException)
        {
            // Auriga's Japanese item DB commonly uses CP932 without a BOM.
        }

        return (Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), 0);
    }

    private static bool TryReadUtf16Newline(byte[] bytes, int offset, int codePage, out string? newline, out int byteLength)
    {
        newline = null;
        byteLength = 0;
        if (offset + 1 >= bytes.Length)
            return false;
        var first = codePage == 1200 ? bytes[offset] : bytes[offset + 1];
        var second = codePage == 1200 ? bytes[offset + 1] : bytes[offset];
        if (second != 0 || first is not (0x0A or 0x0D))
            return false;

        if (first == 0x0D && offset + 3 < bytes.Length)
        {
            var nextFirst = codePage == 1200 ? bytes[offset + 2] : bytes[offset + 3];
            var nextSecond = codePage == 1200 ? bytes[offset + 3] : bytes[offset + 2];
            if (nextFirst == 0x0A && nextSecond == 0)
            {
                newline = "\r\n";
                byteLength = 4;
                return true;
            }
        }

        newline = first == 0x0D ? "\r" : "\n";
        byteLength = 2;
        return true;
    }
}

internal sealed record TextLine(int Start, int ContentLength, int NewlineLength, string Text);
