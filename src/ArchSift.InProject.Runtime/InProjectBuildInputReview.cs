using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchSift.Core;

namespace ArchSift.InProject.Runtime;

/// <summary>Accepts only bounded property conditions whose inputs can be retained and re-evaluated.</summary>
public static class InProjectBuildInputReview
{
    private static readonly Regex CiCondition = new(
        @"^\s*'\$\(CI\)'\s*(?:==|!=)\s*'(?:true|false)'\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ProjectNameCondition = new(
        @"^\s*\$\(MSBuildProjectName\.EndsWith\('[A-Za-z0-9.]+'\)\)" +
        @"(?:\s+Or\s+\$\(MSBuildProjectName\.EndsWith\('[A-Za-z0-9.]+'\)\))*\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex WarningCodes = new(
        @"^\$\((?:NoWarn|WarningsAsErrors)\)(?:;[A-Za-z][A-Za-z0-9]*)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static void Validate(XDocument document, string relativePath)
    {
        foreach (var node in document.Descendants())
        {
            foreach (var attribute in node.Attributes())
            {
                if (attribute.Name.LocalName == "Condition")
                {
                    if (!CiCondition.IsMatch(attribute.Value) &&
                        !ProjectNameCondition.IsMatch(attribute.Value))
                        throw new ConfigurationException("Unreviewed conditional build input: " + relativePath);
                }
                else if (attribute.Value.Contains("$(", StringComparison.Ordinal))
                    throw new ConfigurationException("Unreviewed build property expression: " + relativePath);
            }
            if (!node.HasElements && node.Value.Contains("$(", StringComparison.Ordinal) &&
                !ReviewedPropertyValue(node))
                throw new ConfigurationException("Unreviewed build property expression: " + relativePath);
        }
    }

    private static bool ReviewedPropertyValue(XElement node) =>
        node.Name.LocalName switch
        {
            "NoWarn" => WarningCodes.IsMatch(node.Value) && node.Value.StartsWith("$(NoWarn)", StringComparison.Ordinal),
            "WarningsAsErrors" => WarningCodes.IsMatch(node.Value) &&
                node.Value.StartsWith("$(WarningsAsErrors)", StringComparison.Ordinal),
            "RepositoryRoot" => node.Value == "$(MSBuildThisFileDirectory)",
            _ => false
        };
}
