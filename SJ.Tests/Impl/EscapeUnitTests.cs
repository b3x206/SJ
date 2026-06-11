using SJ.Tests.Base;
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
        foreach (var asciiOnly in new bool[] { false, true })
        {
            sb.Clear(); sbUnescaped.Clear(); // Clear to avoid invalid length

            int escapeCount = SJEscape.Escape(sb, static (s, c) => s.Append(c), content, asciiOnly);
            Assert.AreEqual(sb.Length, escapeCount, $"Expected {sb.Length}, got {escapeCount} for:\n{sb}");

            int unescapeCount = SJEscape.Unescape(sbUnescaped, static (s, c) => s.Append(c), sb.ToString());
            Assert.AreEqual(sbUnescaped.Length, unescapeCount, $"Expected {sbUnescaped.Length}, got {unescapeCount} for :\n{sbUnescaped}");

            // content Escape -> content Unescape must equal itself
            Assert.AreEqual(content, sbUnescaped.ToString());
        }
    }

    // Very long strings may cause problems.
    public static IEnumerable<string[]> TruncatedDataProcessors => TestEx.CreateTruncatedDataProcessors([[EscapeContent], [EscapeInvalidSurrogate], [EscapeInvalidUnescape]]);

    [TestMethod]
    [DynamicData(nameof(TruncatedDataProcessors))]
    [DataRow(EscapeContentEmojiTest)]
    [Timeout(TestTimeout.Short)]
    public void TestWith(string data) => TestEscapeUnescape(data);

    [TestMethod]
    [DataRow(EscapeContentEmojiTest)]
    [DynamicData(nameof(TruncatedDataProcessors))]
    [Timeout(TestTimeout.Short)]
    public void TestTruncated(string data) => TestEscapeUnescape(data);

    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestBrokenUnescape()
    {
        Assert.IsTrue(!SJEscape.Unescape(EscapeInvalidUnescape).Contains('\\'), "Behaviour : Should not contain '\\' token from invalid escapes (as it's removed and the invalid escape is written as-is)");
    }
}
