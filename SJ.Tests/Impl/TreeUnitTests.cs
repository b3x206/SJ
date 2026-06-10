using static SJ.Tests.TestData;

namespace SJ.Tests;

/// <summary>
/// Tests one of the provided examples.
/// </summary>
[TestClass]
public sealed class TreeUnitTests
{
    [TestMethod]
    public void TestTreeFrom()
    {
        var reader = new SJStringReader(JsonData2) { ThrowOnError = true };
        var tree = SJTree.FromJSON(reader);

        Console.WriteLine($"Parsed Tree : {tree}");
    }

    [TestMethod]
    public void TestTreeTo()
    {
        var reader = new SJStringReader(JsonData2) { ThrowOnError = true };
        var tree = SJTree.FromJSON(reader);

        Console.WriteLine($"Tree as JSON : {SJTree.ToJSON(tree)}");
    }
}
