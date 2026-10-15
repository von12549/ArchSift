using System.Security.Cryptography;
using System.Text;

namespace ArchSift.Contracts;

public static class InProjectTestCaseIdentity
{
    public static string ForRule(string ruleKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ruleKey)))[..24].ToLowerInvariant();
}
