using System.Text;
using static SJ.Tests.TestData;

namespace SJ.Tests.Impl;

[TestClass]
public sealed class EscapeUnitTests
{
    static void TestEscapeUnescape(string content)
    {
        var sb = new StringBuilder(content.Length * 2);
        var sbUnescaped = new StringBuilder(content.Length);
        foreach (var opts in Enum.GetValues<SJEscape.EscapeOptions>())
        {
            sb.Clear(); sbUnescaped.Clear(); // Clear to avoid invalid length

            int escapeCount = SJEscape.Escape(sb, static (s, c) => s.Append(c), content, opts);
            Assert.AreEqual(sb.Length, escapeCount, $"Expected {sb.Length}, got {escapeCount} for:\n{sb}");

            int unescapeCount = SJEscape.Unescape(sbUnescaped, static (s, c) => s.Append(c), sb.ToString());
            Assert.AreEqual(sbUnescaped.Length, unescapeCount, $"Expected {sbUnescaped.Length}, got {unescapeCount} for :\n{sbUnescaped}");

            // content Escape -> content Unescape must equal itself
            Assert.AreEqual(content, sbUnescaped.ToString());
        }
    }

    [TestMethod]
    [DataRow(EscapeContent)]
    [DataRow(EscapeContentEmojiTest)]
    [Timeout(TestTimeout.Short)]
    public void TestWith(string data) => TestEscapeUnescape(data);
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestBrokenUnescape()
    {
        string unescaped = SJEscape.Unescape(EscapeBrokenUnescape);
        Assert.IsTrue(!unescaped.Contains('\\'), "Behaviour : Should not contain '\\' token from invalid escapes (as it's removed and the invalid escape is written as-is)");
    }
    // Escape throws when a broken surrogate pair is given. It isn't a "no except" method.
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    [Timeout(TestTimeout.Short)]
    public void TestBrokenSurrogatePair() => TestEscapeUnescape(EscapeBrokenSurrogate);
}
