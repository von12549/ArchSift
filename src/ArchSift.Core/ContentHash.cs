using System.Security.Cryptography;
using System.Text;

namespace ArchSift.Core;

public static class ContentHash
{
    public static string Bytes(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string Text(string text) => Bytes(Encoding.UTF8.GetBytes(text));
}
