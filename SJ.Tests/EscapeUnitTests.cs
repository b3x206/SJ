using static SJ.Tests.TestData;

namespace SJ.Tests;

[TestClass]
public sealed class EscapeUnitTests
{
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
    public void TestBrokenBasic() => TestEscapeUnescape(EscapeBrokenUnescape);
    [TestMethod]
    public void TestBrokenUnescape()
    {
        string unescaped = SJEscape.Unescape(EscapeBrokenUnescape);
        Assert.IsTrue(!unescaped.Contains('\\'), "Behaviour : Should not contain '\\' token from invalid escapes (as it's removed and the invalid escape is written as-is)");
    }

    // Escape throws when a broken surrogate pair is given. It isn't a "no except" method.
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestBrokenSurrogatePair() => TestEscapeUnescape(EscapeBrokenSurrogate);
}
