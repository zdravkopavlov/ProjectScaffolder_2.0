using System.Text;
using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Core.Generation;

public static class TextFileEncoder
{
    public static byte[] Encode(string content, TextFileEncodingKind encodingKind, LineEndingKind lineEndings)
    {
        ArgumentNullException.ThrowIfNull(content);
        string normalized = NormalizeLineEndings(content, lineEndings);
        Encoding encoding = GetEncoding(encodingKind);
        byte[] preamble = encoding.GetPreamble();
        byte[] body = encoding.GetBytes(normalized);

        if (preamble.Length == 0)
        {
            return body;
        }

        byte[] result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    public static string NormalizeLineEndings(string content, LineEndingKind lineEndings)
    {
        string lfContent = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return lineEndings == LineEndingKind.CrLf
            ? lfContent.Replace("\n", "\r\n", StringComparison.Ordinal)
            : lfContent;
    }

    private static Encoding GetEncoding(TextFileEncodingKind encodingKind)
    {
        return encodingKind switch
        {
            TextFileEncodingKind.Utf8 => new UTF8Encoding(false, true),
            TextFileEncodingKind.Utf8Bom => new UTF8Encoding(true, true),
            TextFileEncodingKind.Utf16LittleEndian => new UnicodeEncoding(false, true, true),
            TextFileEncodingKind.Windows1252 => GetWindows1252(),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingKind), encodingKind, "Unsupported text encoding."),
        };
    }

    private static Encoding GetWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
