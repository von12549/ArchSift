using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using ArchSift.Contracts;

namespace ArchSift.Core;

public static class AssemblyArtifacts
{
    public static AssemblyInputs Existing(RunConfiguration config, ProjectSnapshot snapshot)
    {
        if (config.Build.AssemblyManifest is not { } manifestPath)
            return Unbound(config.Build.AssemblyPaths, config.Build, ["No source/build-bound assembly manifest."]);
        var bytes = File.ReadAllBytes(manifestPath);
        if (bytes.Length > RuleLoader.MaximumJsonBytes) throw new ConfigurationException("Assembly manifest exceeds 4 MiB.");
        using var document = JsonDocument.Parse(bytes);
        SchemaValidation.Validate(document.RootElement, "assembly-manifest");
        var manifest = document.RootElement.Deserialize<AssemblyManifest>(JsonContract.Options)!;
        var limits = new List<string>();
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var paths = new List<string>(); var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var bound = true;
        if (snapshot.Projects.Any(p => !p.Supported || !p.FrameworkComplete || !p.ReferencesComplete || !p.PackagesComplete ||
            p.Limitations.Any(l => l.Contains("Explicit build input", StringComparison.Ordinal) || l.Contains("AssemblyName", StringComparison.Ordinal) ||
                l.Contains("Build context", StringComparison.Ordinal))))
        { bound = false; limits.Add("Unsupported/unreproducible source or build facts prevent current-source binding."); }
        if (manifest.InputIdentity.Sha256 != snapshot.InputIdentity.Sha256 ||
            !SameFiles(manifest.InputIdentity.Files, snapshot.InputIdentity.Files)) { bound = false; limits.Add("Manifest does not bind the current source inputs."); }
        if (manifest.BuildInputIdentity.Files.Length == 0 ||
            manifest.BuildInputIdentity.Files.Any(f => !snapshot.InputIdentity.Files.Any(s => s.Path == f.Path && s.Sha256 == f.Sha256 && s.Length == f.Length)))
        { bound = false; limits.Add("Build inputs are empty or cannot be reproduced within TargetRoot."); }
        if (manifest.BuildInputIdentity.Sha256 != FileIdentity(manifest.BuildInputIdentity.Files))
        { bound = false; limits.Add("Build input identity hash is inconsistent."); }
        if (config.Build.TargetFramework is { } tfm && tfm != manifest.TargetFramework ||
            config.Build.Configuration != manifest.Configuration)
        { bound = false; limits.Add("TFM/Configuration differs from manifest."); }
        var seenProjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in manifest.Assemblies)
        {
            var path = PathSafety.Under(directory, assembly.Path);
            if (!File.Exists(path)) { bound = false; limits.Add("Missing assembly: " + assembly.AssemblyName); continue; }
            var metadata = Inspect(path);
            if (FileHash(path) != assembly.Sha256 || metadata.Name != assembly.AssemblyName)
            { bound = false; limits.Add("Assembly hash/name mismatch: " + assembly.AssemblyName); }
            if (assembly.SourceProject != "@dependency" &&
                (metadata.Framework != FrameworkAttribute(manifest.TargetFramework) || metadata.Configuration != manifest.Configuration))
            { bound = false; limits.Add("Assembly metadata context mismatch: " + assembly.AssemblyName); }
            if (assembly.SourceProject != "@dependency")
            {
                var project = snapshot.Projects.SingleOrDefault(p => p.Id == assembly.SourceProject);
                if (project is null || project.AssemblyName != assembly.AssemblyName || !project.Frameworks.Contains(manifest.TargetFramework, StringComparer.Ordinal))
                { bound = false; limits.Add("Project/assembly/TFM mapping cannot be bound: " + assembly.SourceProject); }
                else seenProjects.Add(project.Id);
            }
            paths.Add(path); hashes[path] = assembly.Sha256;
        }
        if (snapshot.Projects.Any(p => p.Supported && !seenProjects.Contains(p.Id)))
        { bound = false; limits.Add("Manifest does not cover all discovered supported projects."); }
        if (manifest.Configuration == "Release") limits.Add("Release optimization may remove type dependencies.");
        return new(paths.ToArray(), hashes, bound, bound && limits.Count == 0, limits.ToArray(), manifest.Sdk,
            manifest.TargetFramework, manifest.Configuration);
    }

    public static AssemblyInputs Unbound(string[] paths, BuildSettings build, string[] limits)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var existing = new List<string>(); var messages = new List<string>(limits);
        foreach (var path in paths)
        {
            PathSafety.EnsureNoLinks(path);
            if (!File.Exists(path)) { messages.Add("Missing assembly: " + Path.GetFileName(path)); continue; }
            existing.Add(Path.GetFullPath(path)); hashes[Path.GetFullPath(path)] = FileHash(path);
        }
        return new(existing.ToArray(), hashes, false, false, messages.ToArray(), null, build.TargetFramework, build.Configuration);
    }

    public static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string FileIdentity(InputFile[] files) => ContentHash.Text(string.Join("\n",
        files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => $"{f.Path}\0{f.Length}\0{f.Sha256}")));
    private static bool SameFiles(InputFile[] a, InputFile[] b) => a.OrderBy(f => f.Path, StringComparer.Ordinal)
        .Select(f => (f.Path, f.Sha256, f.Length)).SequenceEqual(b.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => (f.Path, f.Sha256, f.Length)));
    private static string FrameworkAttribute(string tfm) => ".NETCoreApp,Version=v" + tfm[3..];

    public static (string Name, string? Framework, string? Configuration, string[] References) Inspect(string path)
    {
        using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
        if (!pe.HasMetadata) throw new BadImageFormatException("Not a managed assembly: " + Path.GetFileName(path));
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly) throw new BadImageFormatException("Assembly metadata is missing.");
        var name = reader.GetString(reader.GetAssemblyDefinition().Name);
        string? framework = null, configuration = null;
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            var attributeName = reader.GetString(type.Name);
            if (attributeName is not ("TargetFrameworkAttribute" or "AssemblyConfigurationAttribute")) continue;
            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;
            var value = blob.ReadSerializedString();
            if (attributeName == "TargetFrameworkAttribute") framework = value;
            else configuration = value;
        }
        return (name, framework, configuration, reader.AssemblyReferences.Select(r => reader.GetString(reader.GetAssemblyReference(r).Name)).ToArray());
    }
}
