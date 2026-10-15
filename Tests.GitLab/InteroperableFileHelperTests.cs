using Apps.GitLab.Utils;

namespace Tests.GitLab;

[TestClass]
public class InteroperableFileHelperTests
{
    [TestMethod]
    public void AddMetadata_TranslatedPoXliffWithPluralEntry_KeepsEachTranslationInItsOwnEntry()
    {
        using var xliff = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "interoperable", "messages.vi.xlf"));

        var (file, _, _, _, _) = InteroperableFileHelper.AddMetadata(
            xliff, "messages.po.xlf", "application/xliff+xml", "locale/vi/messages.po",
            "https://gitlab.com/group/repo", "main", "group/repo", "https://gitlab.com",
            DateTimeOffset.UtcNow, null, BlackbirdMetadataType.Target, null);

        var po = new StreamReader(file).ReadToEnd().ReplaceLineEndings("\n");
        Console.WriteLine(po);

        StringAssert.Contains(po, "msgid \"Save\"\nmsgstr \"Lưu\"");
        StringAssert.Contains(po, "msgid \"{0} file\"\nmsgid_plural \"{0} files\"\nmsgstr[0] \"{0} tệp\"\n\n");
        StringAssert.Contains(po, "msgid \"Cancel\"\nmsgstr \"Hủy\"");
        StringAssert.Contains(po, "msgid \"Delete\"\nmsgstr \"Xóa\"");
    }
}
