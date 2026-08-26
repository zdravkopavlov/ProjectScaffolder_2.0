using System.Text;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;

namespace ProjectScaffolder.Core.Tests;

public sealed class TextFileEncoderTests
{
    [Fact]
    public void NormalizesLineEndingsAndWritesUtf8Bom()
    {
        byte[] bytes = TextFileEncoder.Encode("one\ntwo\rthree\r\nfour", TextFileEncodingKind.Utf8Bom, LineEndingKind.CrLf);

        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal("one\r\ntwo\r\nthree\r\nfour", Encoding.UTF8.GetString(bytes[Encoding.UTF8.Preamble.Length..]));
    }

    [Fact]
    public void Windows1252RejectsUnrepresentableCharacters()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            TextFileEncoder.Encode("emoji: 😀", TextFileEncodingKind.Windows1252, LineEndingKind.CrLf));
    }
}
