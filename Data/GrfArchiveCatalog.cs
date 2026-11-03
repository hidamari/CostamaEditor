using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using CostamaEditor.Models;
using CostamaEditor.Services;

namespace CostamaEditor.Data;

public sealed class GrfArchiveCatalog : IDisposable
{
    private readonly object _sync = new();
    private readonly List<ArchiveHandle> _archives = new();

    public IReadOnlyList<SourceLoadResult> Load(IEnumerable<string> orderedPaths)
    {
        lock (_sync)
            return LoadCore(orderedPaths);
    }

    private IReadOnlyList<SourceLoadResult> LoadCore(IEnumerable<string> orderedPaths)
    {
        DisposeArchives();
        var results = new List<SourceLoadResult>();
        var libraryPath = FindLibraryPath();
        if (libraryPath is null)
        {
            return orderedPaths
                .Select(path => new SourceLoadResult(
                    path,
                    LocalizationService.Get("Source.Grf.MissingComponent"),
                    LocalizationService.Get("Source.Grf.InstallHint"),
                    "Source.Grf.MissingComponent",
                    DetailKey: "Source.Grf.InstallHint"))
                .ToList();
        }

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var assembly = Assembly.LoadFrom(libraryPath);
            var utilitiesAssemblyPath = Path.Combine(Path.GetDirectoryName(libraryPath)!, "Utilities.dll");
            var utilitiesAssembly = Assembly.LoadFrom(utilitiesAssemblyPath);
            var encodingService = utilitiesAssembly.GetType("Utilities.Services.EncodingService", throwOnError: true)!;
            var displayEncoding = encodingService.GetProperty("DisplayEncoding", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMemberException(encodingService.FullName, "DisplayEncoding");
            var originalDisplayEncoding = displayEncoding.GetValue(null);

            var holderType = assembly.GetType("GRF.Core.GrfHolder", throwOnError: true)!;
            var constructor = holderType.GetConstructor(new[] { typeof(string) })
                ?? throw new MissingMethodException(holderType.FullName, ".ctor(string)");

            foreach (var path in orderedPaths)
            {
                if (!File.Exists(path))
                {
                    results.Add(new SourceLoadResult(path, LocalizationService.Get("Source.FileNotFound"), StatusKey: "Source.FileNotFound"));
                    continue;
                }

                try
                {
                    displayEncoding.SetValue(null, Encoding.GetEncoding(949));
                    var holder = constructor.Invoke(new object[] { path });
                    var fileTable = GetProperty(holder, "FileTable")
                        ?? throw new InvalidDataException(LocalizationService.Get("Source.Grf.FileTableMissing"));
                    var entries = GetProperty(fileTable, "Entries") as IEnumerable
                        ?? throw new InvalidDataException(LocalizationService.Get("Source.Grf.EntriesMissing"));
                    var handle = new ArchiveHandle(path, holder);
                    foreach (var entry in entries)
                    {
                        if (entry is null)
                            continue;
                        var relativePath = GetProperty(entry, "RelativePath")?.ToString();
                        if (string.IsNullOrWhiteSpace(relativePath))
                            continue;

                        var normalizedPath = relativePath.Replace('/', '\\');
                        if (normalizedPath.Contains("\\item\\", StringComparison.OrdinalIgnoreCase) &&
                            normalizedPath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                        {
                            var key = Path.GetFileNameWithoutExtension(normalizedPath);
                            if (!handle.EntriesByResourceName.TryGetValue(key, out var matches))
                            {
                                matches = new List<IndexedEntry>();
                                handle.EntriesByResourceName.Add(key, matches);
                            }
                            matches.Add(new IndexedEntry(normalizedPath, entry));
                        }
                    }

                    _archives.Add(handle);
                    var header = GetProperty(holder, "Header");
                    var hasErrors = GetProperty(header, "HasErrors") as bool? == true;
                    var hasWarnings = GetProperty(header, "HasWarnings") as bool? == true;
                    var iconCount = handle.EntriesByResourceName.Values.Sum(items => items.Count);
                    var statusKey = hasErrors ? "Source.Grf.OpenedError" : hasWarnings ? "Source.Grf.OpenedWarning" : "Source.Grf.Opened";
                    var diagnostics = GetProperty(header, "Errors") as IEnumerable;
                    var diagnosticText = diagnostics?.Cast<object>()
                        .Select(item => item.ToString())
                        .Where(message => !string.IsNullOrWhiteSpace(message))
                        .Take(3)
                        .ToList();
                    var hasDiagnosticText = diagnosticText is { Count: > 0 };
                    var detailKey = !hasErrors && !hasWarnings
                        ? "Source.Grf.Indexed"
                        : hasDiagnosticText
                            ? "Source.Grf.IndexedWarning"
                            : hasErrors ? "Source.Grf.IndexedError" : "Source.Grf.IndexedWithWarning";
                    var detailArguments = detailKey == "Source.Grf.IndexedWarning"
                        ? new object?[] { iconCount, string.Join("；", diagnosticText!) }
                        : new object?[] { iconCount };
                    results.Add(new SourceLoadResult(
                        path,
                        LocalizationService.Get(statusKey),
                        LocalizationService.Get(detailKey, detailArguments),
                        statusKey,
                        DetailKey: detailKey,
                        DetailArguments: detailArguments));
                }
                catch (Exception ex)
                {
                    results.Add(new SourceLoadResult(path, LocalizationService.Get("Source.OpenFailed"), Unwrap(ex).Message, "Source.OpenFailed"));
                }
                finally
                {
                    displayEncoding.SetValue(null, originalDisplayEncoding);
                }
            }
        }
        catch (Exception ex)
        {
            var detail = Unwrap(ex).Message;
            return orderedPaths.Select(path => new SourceLoadResult(
                path,
                LocalizationService.Get("Source.Grf.ComponentLoadFailed"),
                detail,
                "Source.Grf.ComponentLoadFailed")).ToList();
        }

        return results;
    }

    public IconLookupResult FindIcon(string resourceName)
    {
        lock (_sync)
            return FindIconCore(resourceName);
    }

    private IconLookupResult FindIconCore(string resourceName)
    {
        var fileName = Path.GetFileNameWithoutExtension(resourceName.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
            return new IconLookupResult(null, null, null, LocalizationService.Get("Pool.Icon.NoResource"));

        var targetSuffix = $"\\item\\{fileName}.bmp";
        string? lastError = null;
        foreach (var archive in _archives)
        {
            if (!archive.EntriesByResourceName.TryGetValue(fileName, out var matches))
                continue;

            foreach (var candidate in matches.Where(entry => entry.Path.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var declaredSize = GetProperty(candidate.Entry, "SizeDecompressed");
                    if (declaredSize is not null && Convert.ToInt64(declaredSize, System.Globalization.CultureInfo.InvariantCulture) > 16 * 1024 * 1024)
                        throw new InvalidDataException(LocalizationService.Get("Icon.TooLarge"));
                    var method = candidate.Entry.GetType().GetMethod("GetDecompressedData", BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new MissingMethodException(candidate.Entry.GetType().FullName, "GetDecompressedData");
                    var bytes = method.Invoke(candidate.Entry, null) as byte[]
                        ?? throw new InvalidDataException(LocalizationService.Get("Icon.NoData"));
                    if (bytes.Length < 2 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
                        throw new InvalidDataException(LocalizationService.Get("Icon.NotBmp"));
                    return new IconLookupResult(bytes, archive.Path, candidate.Path, null);
                }
                catch (Exception ex)
                {
                    lastError ??= LocalizationService.Get("Pool.Icon.DisplayFailed", Unwrap(ex).Message);
                }
            }
        }

        if (lastError is not null)
            return new IconLookupResult(null, null, null, lastError);
        return new IconLookupResult(null, null, null, LocalizationService.Get("Pool.Icon.NotFound"));
    }

    public void Dispose()
    {
        lock (_sync)
            DisposeArchives();
    }

    private static string? FindLibraryPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GRF Editor", "GRF.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GRF Editor", "GRF.dll"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private void DisposeArchives()
    {
        foreach (var archive in _archives)
        {
            if (archive.Holder is IDisposable disposable)
                disposable.Dispose();
        }
        _archives.Clear();
    }

    private static object? GetProperty(object? instance, string name) =>
        instance?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException!
            : exception;

    private sealed record IndexedEntry(string Path, object Entry);

    private sealed class ArchiveHandle(string path, object holder)
    {
        public string Path { get; } = path;
        public object Holder { get; } = holder;
        public Dictionary<string, List<IndexedEntry>> EntriesByResourceName { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record IconLookupResult(byte[]? Bytes, string? ArchivePath, string? EntryPath, string? Error);
