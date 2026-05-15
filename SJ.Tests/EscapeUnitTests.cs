using System.Text;
using static SJ.Tests.TestData;

namespace SJ.Tests;

[TestClass]
public sealed class EscapeUnitTests
{
    static void TestEscapeUnescape(string content)
    {
        var sb = new StringBuilder(content.Length * 2);
        var sbUnescaped = new StringBuilder(content.Length);
        foreach (var opts in Enum.GetValues<SJEscape.EscapeOptions>())
        {
            int escapeCount = SJEscape.Escape(sb, static (s, c) => s.Append(c), content, opts);
            Assert.AreEqual(sb.Length, escapeCount);

            int unescapeCount = SJEscape.Unescape(sbUnescaped, static (s, c) => s.Append(c), content);
            Assert.AreEqual(sbUnescaped.Length, unescapeCount);
            Console.WriteLine($"unescaped : {sbUnescaped}, content: {content}, len(unescaped: {unescapeCount})");

            Assert.AreEqual(content, sbUnescaped.ToString());
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
