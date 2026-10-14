using System;
using System.Collections.Generic;
using ArchSift.InProject.TestBridge;
using Xunit;

namespace ArchSift.InProject.ArchitectureTests;

public sealed class ArchitectureRules
{
    public static IEnumerable<object[]> Cases => ArchitectureRuleBridge.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate(string ruleKey)
    {
        var result = ArchitectureRuleBridge.Execute(ruleKey);
        Assert.True(result.Status is "pass" or "not-applicable",
            $"{ruleKey}: {result.Status}; findings={string.Join(',', result.FindingIds)}; " +
            string.Join("; ", result.Limitations));
    }
}
