using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.Setup;

public static class SetupFiles
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string FileHash(string path) { Safe(path); using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(); }
    public static string JsonHash<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, JsonContract.Options));
    public static T Read<T>(string path)
    {
        Safe(path);
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new ConfigurationException("Setup JSON exceeds 4 MiB.");
        return Parse<T>(File.ReadAllBytes(path));
    }
    public static T Parse<T>(byte[] bytes)
    {
        if (bytes.Length > 4 * 1024 * 1024) throw new ConfigurationException("Setup JSON exceeds 4 MiB.");
        using var doc = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
        CheckKeys(doc.RootElement);
        using var schemaStream = typeof(SetupFiles).Assembly.GetManifestResourceStream("ArchSift.Setup.Schemas.setup.schema.json")!;
        using var schemaDocument = JsonDocument.Parse(schemaStream);
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(schemaDocument.RootElement.GetRawText())!;
        schema["$ref"] = JsonSerializer.SerializeToElement("#/$defs/" + typeof(T).Name);
        SchemaValidation.ValidateDocument(doc.RootElement, JsonSerializer.SerializeToElement(schema));
        return doc.RootElement.Deserialize<T>(JsonContract.Options) ?? throw new ConfigurationException("Null setup JSON.");
    }
    public static void CheckKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in element.EnumerateObject())
            { if (!names.Add(prop.Name)) throw new ConfigurationException("Duplicate JSON key: " + prop.Name); CheckKeys(prop.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckKeys(item);
    }
    public static string Safe(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
            throw new ConfigurationException("Remote/device roots are unsupported.");
        PathSafety.EnsureNoLinks(full);
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(full)!).DriveType != DriveType.Fixed)
            throw new ConfigurationException("A fixed local volume is required.");
        return full;
    }
    public static string Under(string root, string relative) { ValidateRelative(relative); return PathSafety.Under(Safe(root), relative); }
    public static void ValidateRelative(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains(':'))
            throw new ConfigurationException("Unsafe package/state path.");
        foreach (var part in relative.Split('/'))
        {
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(c => c < 32 || "<>\"|?*".Contains(c)))
                throw new ConfigurationException("Unsafe package/state path.");
            var stem = part.Split('.')[0];
            if (new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(stem, "^(COM|LPT)[0-9¹²³]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new ConfigurationException("Reserved Windows path.");
        }
    }
    public static void Atomic<T>(string path, T value, bool replace = true) => AtomicBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, JsonContract.Options), replace);
    public static void AtomicBytes(string path, byte[] bytes, bool replace = true)
    {
        Safe(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); Safe(path);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { file.Write(bytes); file.Flush(flushToDisk: true); }
        Safe(path); File.Move(temporary, path, replace);
    }
    public static StateIdentity Capture(string config, string library)
    {
        Safe(config); Safe(library);
        var files = new List<StateFile>();
        if (File.Exists(config)) files.Add(new("config", "config.json", FileHash(config)));
        if (Directory.Exists(library))
        {
            void Walk(string directory)
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
                {
                    Safe(path);
                    if (Directory.Exists(path)) Walk(path);
                    else { var relative = Path.GetRelativePath(library, path).Replace('\\', '/'); ValidateRelative(relative); files.Add(new("library", relative, FileHash(path))); }
                    if (files.Count > 10000) throw new ConfigurationException("State file-count limit exceeded.");
                }
            }
            Walk(library);
        }
        var sorted = files.OrderBy(f => f.Scope, StringComparer.Ordinal).ThenBy(f => f.Path, StringComparer.Ordinal).ToArray();
        return new(File.Exists(config), Directory.Exists(library), sorted, JsonHash(new { configExists = File.Exists(config), libraryExists = Directory.Exists(library), files = sorted }));
    }
    public static void Backup(string config, string library, StateIdentity state, string destination)
    {
        long total = 0;
        foreach (var file in state.Files)
        {
            var input = file.Scope == "config" ? Safe(config) : Under(library, file.Path);
            if ((total += new FileInfo(input).Length) > 2L * 1024 * 1024 * 1024) throw new ConfigurationException("Backup exceeds 2 GiB.");
            var output = Under(destination, file.Scope + "/" + file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); Safe(output);
            File.Copy(input, output, false);
            if (FileHash(output) != file.Sha256) throw new IOException("State changed while backing up.");
        }
        if (Capture(config, library).Sha256 != state.Sha256) throw new IOException("State changed while backing up.");
    }
    public static string HostHash()
    {
        var records = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var scope in new[] { EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables(scope)) records[scope + "/" + entry.Key] = (string?)entry.Value ?? "";
        }
        records["process-path"] = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (OperatingSystem.IsWindows())
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            foreach (var folder in new[] { Path.Combine(documents, "PowerShell"), Path.Combine(documents, "WindowsPowerShell"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0") })
                foreach (var name in new[] { "profile.ps1", "Microsoft.PowerShell_profile.ps1" })
                { var path = Path.Combine(folder, name); records[path] = File.Exists(path) ? FileHash(path) : "absent"; }
        }
        return JsonHash(records);
    }
    public static void HostCheckpoint(string baseline)
    {
        var current = HostHash(); var equal = current == baseline;
        Console.Error.WriteLine("host-state-sha256=" + current + " host-state-equal=" + equal.ToString().ToLowerInvariant());
        if (!equal) throw new IOException("Host drift detected; preserve evidence and stop. No repair is permitted.");
    }
}
