using System.Text.Json;
using System.Transactions;
using ArchSift.ArchUnit;
using ArchSift.Contracts;
using ArchSift.Core;

namespace ArchSift.UnitTests;

public sealed class AsyncStateMachineDependencyTests
{
    private static readonly string[] Paths = Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
        .Where(path => { try { _ = System.Reflection.AssemblyName.GetAssemblyName(path); return true; } catch (BadImageFormatException) { return false; } }).ToArray();
    private static readonly Lazy<TypeDependencyTestContext> Context = new(() => TypeDependencyTestEngine.LoadContext(Paths));

    [Theory]
    [InlineData("UnsafeAsync", "violation")]
    [InlineData("SafeAsync", "pass")]
    [InlineData("GenericAsync`1", "violation")]
    [InlineData("LocalAsync", "violation")]
    [InlineData("Absent", "inconclusive")]
    public void AsyncDependenciesBelongToTheOriginalType(string name, string expected)
    {
        var result = TypeDependencyTestEngine.Evaluate(Bundle(name), Context.Value);
        Assert.Equal(expected, Assert.Single(result.Results).Status);
        Assert.All(result.Findings, f => { Assert.EndsWith(name, f.Source); Assert.Equal("System.Transactions.TransactionScope", f.Target); });
    }

    [Fact]
    public void WorkerDetectsAsyncCallsButDoesNotPromoteUnboundAssemblies()
    {
        var bundle = Bundle("UnsafeAsync");
        var inputs = new AssemblyInputs(Paths, Paths.ToDictionary(p => p, p => AssemblyArtifacts.FileHash(p)), false, true,
            [], null, "net10.0", "Release");
        var result = AssemblyWorker.Evaluate(new(inputs, [bundle.Documents[0].Ruleset]));
        Assert.Empty(result.Errors);
        Assert.Equal("inconclusive", Assert.Single(result.Results).Status);
        Assert.Contains(result.Findings, f => f.Target == "System.Transactions.TransactionScope" && f.Source!.EndsWith("UnsafeAsync", StringComparison.Ordinal));
    }

    private static RuleBundle Bundle(string name)
    {
        var source = new Selector { Kind = "type", Match = "exact", Value = "ArchSift.UnitTests.AsyncStateMachineDependencyTests+" + name };
        var rule = new Rule { Id = "async-regression", Type = "type-dependency", Enabled = true, Scope = source,
            Parameters = JsonSerializer.SerializeToElement(new { source, forbiddenTarget = new Selector { Kind = "type", Match = "exact", Value = "System.Transactions.TransactionScope" } }, JsonContract.Options), Severity = "warning", Reason = "Async dependency regression" };
        var document = new Ruleset { SchemaVersion = 1, Id = "async-tests", Version = "1", Description = "Synthetic; methods never run", Rules = [rule], Exceptions = [] };
        return new([new(document, new(document.Id, document.Version, "synthetic", "synthetic"))]);
    }

    public sealed class UnsafeAsync
    {
        public async Task Work() { await Task.Yield(); using var scope = new TransactionScope(); scope.Complete(); }
    }
    public sealed class SafeAsync
    {
        public async Task Work() { await Task.Yield(); _ = new Lookalike.TransactionScope(); }
    }
    public sealed class GenericAsync<T>
    {
        public async Task Work(T value) { await Task.Yield(); using var scope = new TransactionScope(); scope.Complete(); }
    }
    public sealed class LocalAsync
    {
        public Task Work()
        {
            var captured = 0;
            async Task Local() { await Task.Yield(); captured++; using var scope = new TransactionScope(); scope.Complete(); }
            return Local();
        }
    }
    public static class Lookalike { public sealed class TransactionScope { } }
}
