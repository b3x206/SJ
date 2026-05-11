
namespace SJ.Tests;

[TestClass]
public sealed class EscapeUnitTests
{
    // Not much to test here.
    const string EscapeContent = "Quote: \", Backslash: \\, Tab: \t, Newline: \n, Pizza: \uD83C\uDF55, The 🅱 variant: \uD83C\uDD71\uFE0F";
    const string BrokenUnescapeContent = "Oh wow, I'm escaping things I shouldn't! : \\a\\b\\c\\d \\e\\f\\g\\h\\i\\j\\k \\l\\m\\n\\o\\p\\q\\r\\s\\ t\\u\\v\\w\\x\\y\\z\\A\\B\\C \\D\\E\\F\\G\\H\\I\\J\\K\\L\\M\\N\\O\\P\\Q\\ R\\S\\T\\U\\V \\W\\X \\Y\\Z'";
    const string BrokenSurrogatePair = "\uDF55\uD83C\uDD71\uD83C\uFE0F In C++, surrogate escape you!";

    static void TestEscapeUnescape(string content)
    {
        foreach (var opts in Enum.GetValues<SJEscape.EscapeOptions>())
        {
            string escaped = SJEscape.Escape(content, opts);
            string unescaped = SJEscape.Unescape(escaped);
            Assert.AreEqual(content, unescaped);
        }
    }

    [TestMethod]
    public void TestBasic() => TestEscapeUnescape(EscapeContent);
    [TestMethod]
    public void TestBrokenBasic() => TestEscapeUnescape(BrokenUnescapeContent);
    [TestMethod]
    public void TestBrokenUnescape()
    {
        string unescaped = SJEscape.Unescape(BrokenUnescapeContent);
        Assert.IsTrue(!unescaped.Contains('\\'), "Behaviour : Should not contain '\\' token from invalid escapes (as it's removed and the invalid escape is written as-is)");
    }

    // Escape throws when a broken surrogate pair is given. It isn't a "no except" method.
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestBrokenSurrogatePair() => TestEscapeUnescape(BrokenSurrogatePair);
}
