using Apps.GitLab.Constants;
using Apps.GitLab.Utils.File;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Filters.Bilingual.Xliff1;
using System.Text;
using System.Xml.Linq;

namespace Tests.GitLab;

[TestClass]
public class FileHelperTests
{
    private const string Html = "<html><head></head><body><p>Hello</p></body></html>";

    [TestMethod]
    [DataRow(null)]
    [DataRow(OutputFileTypes.Original)]
    public async Task ProcessDownloadedFile_Original_PreservesFormatAndAddsMetadata(string? outputFileType)
    {
        var result = FileHelper.ProcessDownloadedFile(
            CreateDownloadedFile(Html, "folder/sample.html"),
            null,
            "en-US",
            "content-123",
            "Sample",
            outputFileType);

        Assert.AreEqual("sample.html", result.FileName);
        Assert.AreEqual("text/html", result.MimeType);

        var content = await ReadToEnd(result.FileStream);
        StringAssert.Contains(content, "lang=\"en-US\"");
        StringAssert.Contains(content, "name=\"blackbird-ucid\" content=\"content-123\"");
        StringAssert.Contains(content, "name=\"blackbird-content-name\" content=\"Sample\"");
        StringAssert.Contains(content, "name=\"blackbird-system-name\" content=\"Gitlab\"");
    }

    [TestMethod]
    public async Task ProcessDownloadedFile_Xliff1_ReturnsVersion12WithMetadata()
    {
        var result = FileHelper.ProcessDownloadedFile(
            CreateDownloadedFile(Html, "folder/sample.html"),
            null,
            "en-US",
            "content-123",
            "Sample",
            OutputFileTypes.Xliff1);

        Assert.AreEqual("sample.html.xlf", result.FileName);
        Assert.AreEqual("application/x-xliff+xml", result.MimeType);

        var content = await ReadToEnd(result.FileStream);
        var root = XDocument.Parse(content).Root!;
        Assert.AreEqual(Xliff1Serializer.XliffNs.NamespaceName, root.Name.NamespaceName);
        Assert.AreEqual("1.2", root.Attribute("version")?.Value);
        StringAssert.Contains(content, "source-ucid");
        StringAssert.Contains(content, "content-123");
        StringAssert.Contains(content, "source-system-name");
        StringAssert.Contains(content, "Gitlab");
    }

    [TestMethod]
    public async Task ProcessDownloadedFile_Xliff2_ReturnsVersion22WithMetadata()
    {
        var result = FileHelper.ProcessDownloadedFile(
            CreateDownloadedFile(Html, "folder/sample.html"),
            null,
            "en-US",
            "content-123",
            "Sample",
            OutputFileTypes.Xliff2);

        Assert.AreEqual("sample.html.xlf", result.FileName);
        Assert.AreEqual("application/xliff+xml", result.MimeType);

        var content = await ReadToEnd(result.FileStream);
        var root = XDocument.Parse(content).Root!;
        Assert.AreEqual("urn:oasis:names:tc:xliff:document:2.2", root.Name.NamespaceName);
        Assert.AreEqual("2.2", root.Attribute("version")?.Value);
        StringAssert.Contains(content, "source-ucid");
        StringAssert.Contains(content, "content-123");
        StringAssert.Contains(content, "source-system-name");
        StringAssert.Contains(content, "Gitlab");
    }

    [TestMethod]
    public async Task ProcessDownloadedFile_UnsupportedOriginalFormat_ReturnsRawContent()
    {
        const string rawContent = "not a valid DOCX";
        var result = FileHelper.ProcessDownloadedFile(
            CreateDownloadedFile(rawContent, "broken.docx"),
            null,
            null,
            null,
            null);

        Assert.AreEqual("broken.docx", result.FileName);
        Assert.AreEqual(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(rawContent)),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(await ReadToEnd(result.FileStream))));
    }

    [TestMethod]
    public void ProcessDownloadedFile_UnknownOutputType_Throws()
    {
        var exception = Assert.Throws<PluginMisconfigurationException>(() =>
            FileHelper.ProcessDownloadedFile(
                CreateDownloadedFile(Html, "sample.html"),
                null,
                null,
                null,
                null,
                "unknown"));

        StringAssert.Contains(exception.Message, "Unsupported output file type");
    }

    [TestMethod]
    public void ProcessDownloadedFile_UnconvertibleFileToXliff_Throws()
    {
        var exception = Assert.Throws<PluginMisconfigurationException>(() =>
            FileHelper.ProcessDownloadedFile(
                CreateDownloadedFile("not a valid DOCX", "broken.docx"),
                null,
                null,
                null,
                null,
                OutputFileTypes.Xliff2));

        StringAssert.Contains(exception.Message, "could not be converted");
    }

    private static DownloadedFile CreateDownloadedFile(string content, string path)
    {
        return new(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            path,
            "https://gitlab.com/example/repo",
            "main",
            "https://gitlab.com");
    }

    private static async Task<string> ReadToEnd(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
