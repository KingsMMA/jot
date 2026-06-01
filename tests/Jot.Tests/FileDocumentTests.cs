using System.IO;
using System.Text;
using Jot.Editor;
using Xunit;

namespace Jot.Tests;

public class FileDocumentTests
{
    [Fact]
    public void DetectEncoding_Utf8NoBom_DefaultsToUtf8WithoutBom()
    {
        var bytes = Encoding.UTF8.GetBytes("hello");
        var (encoding, hasBom, bomLength) = FileDocument.DetectEncoding(bytes);
        Assert.IsType<UTF8Encoding>(encoding);
        Assert.False(hasBom);
        Assert.Equal(0, bomLength);
    }

    [Fact]
    public void DetectEncoding_Utf8Bom_IsDetected()
    {
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("hi")).ToArray();
        var (_, hasBom, bomLength) = FileDocument.DetectEncoding(bytes);
        Assert.True(hasBom);
        Assert.Equal(3, bomLength);
    }

    [Fact]
    public void DetectEncoding_Utf16LeBom_IsDetected()
    {
        var bytes = new UnicodeEncoding(false, true).GetPreamble()
            .Concat(new UnicodeEncoding(false, false).GetBytes("hi")).ToArray();
        var (encoding, hasBom, bomLength) = FileDocument.DetectEncoding(bytes);
        Assert.IsType<UnicodeEncoding>(encoding);
        Assert.True(hasBom);
        Assert.Equal(2, bomLength);
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", LineEnding.Crlf)]
    [InlineData("a\nb\nc", LineEnding.Lf)]
    [InlineData("a\rb\rc", LineEnding.Cr)]
    [InlineData("no breaks", LineEnding.Crlf)]
    [InlineData("a\r\nb\nc\n", LineEnding.Lf)] // mixed, LF dominant
    public void DetectLineEnding_PicksDominant(string text, LineEnding expected)
    {
        Assert.Equal(expected, FileDocument.DetectLineEnding(text));
    }

    [Theory]
    [InlineData(LineEnding.Crlf, "a\r\nb")]
    [InlineData(LineEnding.Lf, "a\nb")]
    [InlineData(LineEnding.Cr, "a\rb")]
    public void Save_RestoresLineEnding(LineEnding ending, string expectedOnDisk)
    {
        var path = Path.GetTempFileName();
        try
        {
            FileDocument.Save(path, "a\nb", new UTF8Encoding(false), false, ending,
                new SaveOptions(TrimTrailingWhitespace: false, InsertFinalNewline: false));
            var onDisk = File.ReadAllText(path);
            Assert.Equal(expectedOnDisk, onDisk);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_RoundTripsUtf8BomAndCrlf()
    {
        var path = Path.GetTempFileName();
        try
        {
            FileDocument.Save(path, "line1\nline2", new UTF8Encoding(true), true, LineEnding.Crlf,
                new SaveOptions(false, false));
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal("line1\r\nline2", new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_TrimsTrailingWhitespace_WhenEnabled()
    {
        var path = Path.GetTempFileName();
        try
        {
            FileDocument.Save(path, "a   \nb\t\n", new UTF8Encoding(false), false, LineEnding.Lf,
                new SaveOptions(TrimTrailingWhitespace: true, InsertFinalNewline: false));
            Assert.Equal("a\nb\n", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_InsertsFinalNewline_WhenEnabled()
    {
        var path = Path.GetTempFileName();
        try
        {
            FileDocument.Save(path, "a\nb", new UTF8Encoding(false), false, LineEnding.Lf,
                new SaveOptions(TrimTrailingWhitespace: false, InsertFinalNewline: true));
            Assert.Equal("a\nb\n", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LoadSave_PreservesContentExactly_ForCrlfNoBom()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "first\r\nsecond\r\nthird\r\n", new UTF8Encoding(false));
            var doc = FileDocument.Load(path);
            Assert.Equal(LineEnding.Crlf, doc.LineEnding);
            Assert.Equal("first\nsecond\nthird\n", doc.Text); // normalised to LF in memory

            FileDocument.Save(path, doc.Text, doc.Encoding, doc.HasBom, doc.LineEnding,
                new SaveOptions(TrimTrailingWhitespace: false, InsertFinalNewline: false));
            Assert.Equal("first\r\nsecond\r\nthird\r\n", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
