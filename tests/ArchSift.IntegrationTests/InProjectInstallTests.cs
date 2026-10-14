using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchSift.Core;
using ArchSift.InProject.Runtime;

namespace ArchSift.IntegrationTests;

public sealed class InProjectInstallTests
{
    [Fact]
    public void Initialize_UsesOnlyOwnedPathsAndChecksPackageIdentity()
    {
        var fixture = NewFixture();
        var checkout = Path.Combine(fixture, "checkout");
        Directory.CreateDirectory(checkout);
        Git(checkout, "init", "--initial-branch=main");
        var solution = Path.Combine(checkout, "Product.slnx");
        File.WriteAllText(solution, "<Solution />\n");
        var before = LocalPackage.HashFile(solution);
        var package = BuildPackage(fixture);
        var result = InProjectInstaller.Initialize(checkout, package.Path, package.Sha256);
        Assert.Equal(Path.GetFullPath(checkout), result.WorktreeRoot);
        Assert.Equal(before, LocalPackage.HashFile(solution));
        Assert.True(File.Exists(result.ConfigurationPath));
        Assert.True(File.Exists(result.VersionLockPath));
        using var config = JsonDocument.Parse(File.ReadAllText(result.ConfigurationPath));
        Assert.Equal("../Product.slnx", config.RootElement.GetProperty("solution").GetString());
        using var locked = JsonDocument.Parse(File.ReadAllText(result.VersionLockPath));
        Assert.Equal(package.Sha256, locked.RootElement.GetProperty("packageSha256").GetString());
        Assert.Equal(7, locked.RootElement.GetProperty("components").GetArrayLength());
        var ignore = File.ReadAllText(Path.Combine(checkout, ".gitignore"));
        Assert.Contains("/.archsift/runtime/", ignore, StringComparison.Ordinal);
        Assert.Contains("/.archsift/state/", ignore, StringComparison.Ordinal);
        Assert.Contains("/.archsift/reports/", ignore, StringComparison.Ordinal);
        Assert.Contains("/.archsift/evidence/", ignore, StringComparison.Ordinal);
        Assert.DoesNotContain("/.archsift/\n", ignore, StringComparison.Ordinal);
        Assert.Throws<ConfigurationException>(() => InProjectInstaller.Initialize(checkout, package.Path, package.Sha256));
        Assert.Equal("0.1.0", LocalPackage.VerifyInstalled(checkout));
        File.AppendAllText(Path.Combine(checkout, ".archsift", "runtime", "ArchSift.Core.dll"), "tamper");
        Assert.Throws<ConfigurationException>(() => LocalPackage.VerifyInstalled(checkout));
    }

    [Fact]
    public void Initialize_RejectsAmbiguousSolutionAndTamperedDigestBeforeWriting()
    {
        var fixture = NewFixture();
        var checkout = Path.Combine(fixture, "checkout");
        Directory.CreateDirectory(checkout);
        Git(checkout, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(checkout, "One.slnx"), "<Solution />\n");
        File.WriteAllText(Path.Combine(checkout, "Two.slnx"), "<Solution />\n");
        File.WriteAllText(Path.Combine(checkout, ".gitignore"), "# human entry\r\n");
        var package = BuildPackage(fixture);
        Assert.Throws<ConfigurationException>(() => InProjectInstaller.Initialize(checkout, package.Path, package.Sha256));
        Assert.False(Directory.Exists(Path.Combine(checkout, ".archsift")));
        Assert.Throws<ConfigurationException>(() => InProjectInstaller.Initialize(checkout, package.Path, new string('0', 64), "One.slnx"));
        Assert.False(Directory.Exists(Path.Combine(checkout, ".archsift")));
        var result = InProjectInstaller.Initialize(checkout, package.Path, package.Sha256, "Two.slnx");
        using var config = JsonDocument.Parse(File.ReadAllText(result.ConfigurationPath));
        Assert.Equal("../Two.slnx", config.RootElement.GetProperty("solution").GetString());
        Assert.StartsWith("# human entry\r\n", File.ReadAllText(Path.Combine(checkout, ".gitignore")), StringComparison.Ordinal);
    }

    [Fact]
    public void Initialize_InvalidRuleLeavesNoInstallationOrIgnoreChange()
    {
        var fixture = NewFixture();
        var checkout = Path.Combine(fixture, "checkout");
        Directory.CreateDirectory(checkout);
        Git(checkout, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(checkout, "Product.slnx"), "<Solution />\n");
        File.WriteAllText(Path.Combine(checkout, ".gitignore"), "# keep\n");
        var package = BuildPackage(fixture, "{}");
        Assert.Throws<ConfigurationException>(() => InProjectInstaller.Initialize(checkout, package.Path, package.Sha256));
        Assert.False(Directory.Exists(Path.Combine(checkout, ".archsift")));
        Assert.Empty(Directory.GetDirectories(checkout, ".archsift.stage.*"));
        Assert.Equal("# keep\n", File.ReadAllText(Path.Combine(checkout, ".gitignore")));
    }

    [Fact]
    public void Initialize_LinkedWorktreeHasIndependentStateAndGitFile()
    {
        var fixture = NewFixture();
        var main = Path.Combine(fixture, "main");
        var linked = Path.Combine(fixture, "linked");
        Directory.CreateDirectory(main);
        Git(main, "init", "--initial-branch=main");
        File.WriteAllText(Path.Combine(main, "Product.slnx"), "<Solution />\n");
        Git(main, "add", "Product.slnx");
        Git(main, "-c", "user.name=ArchSift Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-m",
            "Synthetic worktree fixture\n\nCo-Authored-By: Codex <noreply@openai.com>");
        Git(main, "worktree", "add", "--detach", linked);
        Assert.True(File.Exists(Path.Combine(linked, ".git")));
        File.AppendAllText(Path.Combine(linked, "Product.slnx"), "<!-- linked dirty input -->\n");
        var package = BuildPackage(fixture);
        var mainResult = InProjectInstaller.Initialize(main, package.Path, package.Sha256);
        var linkedResult = InProjectInstaller.Initialize(linked, package.Path, package.Sha256);
        Assert.NotEqual(mainResult.WorktreeRoot, linkedResult.WorktreeRoot);
        Assert.NotEqual(mainResult.ConfigurationPath, linkedResult.ConfigurationPath);
        Assert.True(Directory.Exists(Path.Combine(main, ".archsift", "state")));
        Assert.True(Directory.Exists(Path.Combine(linked, ".archsift", "state")));
        Assert.NotEqual(LocalPackage.HashFile(Path.Combine(main, "Product.slnx")),
            LocalPackage.HashFile(Path.Combine(linked, "Product.slnx")));
    }

    private static string NewFixture()
    {
        var root = Environment.GetEnvironmentVariable("ARCHSIFT_TEST_LAB_ROOT");
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Path.GetTempPath(), "ArchSift-InProject-fixtures");
        var fixture = Path.GetFullPath(Path.Combine(root, "ip01-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(fixture);
        return fixture;
    }

    private static (string Path, string Sha256) BuildPackage(string fixture, string? rulesJson = null)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var componentPath in LocalPackage.Components.Values)
            files[componentPath] = Encoding.UTF8.GetBytes("synthetic component " + componentPath + "\n");
        files["bootstrap/archsift-inproject.ps1"] = Encoding.UTF8.GetBytes("# synthetic bootstrap fixture\n");
        files["tests/ArchSift.InProject.ArchitectureTests.csproj"] = Encoding.UTF8.GetBytes("<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        files["rules/architecture.json"] = Encoding.UTF8.GetBytes(rulesJson ?? """
            {"schemaVersion":1,"id":"fixture","version":"1.0.0","description":"Synthetic fixture","rules":[{"id":"tfm","type":"target-framework","enabled":true,"scope":{"kind":"project","match":"glob","value":"**"},"parameters":{"allowedFrameworks":["net8.0","net9.0","net10.0"]},"severity":"warning","reason":"Fixture"}],"exceptions":[]}
            """);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, product = "ArchSift.InProject", version = "0.1.0", rid = "win-x64",
            sourceCommit = new string('a', 40), contractVersion = 1,
            files = files.Select(pair => new { path = pair.Key, bytes = pair.Value.LongLength,
                sha256 = Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant() }).ToArray()
        });
        var path = Path.Combine(fixture, "inproject-local.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var pair in files)
            {
                using var stream = archive.CreateEntry(pair.Key).Open();
                stream.Write(pair.Value);
            }
            using var manifestStream = archive.CreateEntry("payload-manifest.json").Open();
            manifestStream.Write(manifest);
        }
        return (path, LocalPackage.HashFile(path));
    }

    private static void Git(string directory, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, string.Join(" ", arguments) + "\n" + output + "\n" + error);
    }
}
