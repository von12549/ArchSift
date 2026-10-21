using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ArchSift.TestGate;

/// <summary>Preserves the redirected Windows runner's console bytes before decoding JSON.</summary>
internal static class DiscoveryOutput
{
    internal static int? WindowsOemCodePage => OperatingSystem.IsWindows() ? checked((int)GetOEMCP()) : null;

    internal static (string Text, string Encoding) Decode(byte[] bytes, int? oemCodePage)
    {
        var utf8 = new UTF8Encoding(false, true);
        string text;
        string encoding;
        try { text = utf8.GetString(bytes); encoding = "utf-8"; }
        catch (DecoderFallbackException) when (oemCodePage is not null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var oem = System.Text.Encoding.GetEncoding(oemCodePage.Value,
                EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            text = oem.GetString(bytes);
            encoding = oem.WebName;
        }
        // Invalid/truncated JSON still fails; this never alters case IDs or arguments.
        using var parsed = JsonDocument.Parse(text.TrimStart('\uFEFF'));
        return (text.TrimStart('\uFEFF'), encoding);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
