using System.Text;
using static SJ.Tests.TestData;

namespace SJ.Tests;

[TestClass]
public sealed class ReaderUnitTests
{
    public static void ReadInner(StringBuilder sb, SJReader reader, SJReader.Value root)
    {
        switch (root.type)
        {
            default:
            case SJType.Error:
                {
                    reader.ThrowError();
                    break;
                }

            case SJType.Array:
                {
                    bool first = true;
                    sb.Append('[');
                    while (reader.IterateArray(root, out var array))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, array);

                        first = false;
                    }
                    sb.Append(']');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.Object:
                {
                    bool first = true;
                    sb.Append('{');
                    while (reader.IterateObject(root, out var k, out var v))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, k);
                        sb.Append(':');
                        ReadInner(sb, reader, v);

                        first = false;
                    }
                    sb.Append('}');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.String:
                {
                    sb.Append('"').Append(root.Slice()).Append('"');
                    break;
                }
            case SJType.Number:
            case SJType.Null:
            case SJType.Bool:
                {
                    sb.Append(root.Slice());
                    break;
                }
        }
    }
    public const int DefaultSbCapacity = 512;
    public static StringBuilder Read(string data)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        Read(sb, data);
        return sb;
    }
    public static void Read(StringBuilder sb, string data)
    {
        sb.Clear();
        var reader = new SJStringReader(data);
        var root = reader.Read();

        ReadInner(sb, reader, root);
    }
    public static StringBuilder ReadJSC(string data)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        ReadJSC(sb, data);
        return sb;
    }
    public static void ReadJSC(StringBuilder sb, string data)
    {
        sb.Clear();
        var reader = new SJStringReader(data)
        {
            allowComments = true
        };
        var root = reader.Read();

        ReadInner(sb, reader, root);
    }
    public static StringBuilder Read(SJReader reader)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        var root = reader.Read();

        ReadInner(sb, reader, root);
        return sb;
    }

    // Basic tests: Read and Empty
    [TestMethod]
    public void TestBasic() => Read(JsonData1);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestBasicInvalid() => Read(JsonDataInvalid);
    [TestMethod]
    public void TestSlightlyComplicated() => Read(JsonData2);
    static void TestDiscard(string discardData, string discardKey = JsonDataDiscardKey)
    {
        // Manual reading
        var reader = new SJStringReader(discardData)
        {
            ThrowOnError = true
        };

        var root = reader.Read();
        while (reader.IterateObject(root, out var k, out var v))
        {
            if (k.Slice().Equals(discardKey, StringComparison.Ordinal))
            {
                Console.WriteLine($"Skipping very secret key '{k.Slice()}'");
                continue;
            }

            switch (v.type)
            {
                default:
                    Console.WriteLine($"{k.Slice()} : {v.Slice()}");
                    break;

                case SJType.Object:
                    Console.WriteLine($"{k.Slice()} :");
                    while (reader.IterateObject(v, out var k2, out var v2))
                    {
                        Console.WriteLine($"  {k2.Slice()} : {v2.Slice()}");
                    }
                    break;
                case SJType.Array:
                    Console.WriteLine($"{k.Slice()} :");
                    while (reader.IterateArray(v, out var av))
                    {
                        Console.WriteLine($"  {av.Slice()}");
                    }
                    break;
            }
        }
    }
    [TestMethod]
    public void TestDiscard() => TestDiscard(JsonDataDiscard);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestDiscardInvalid() => TestDiscard(JsonDataDiscardInvalid);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestStacking() => Read(JsonDataStackingInvalid);
    [TestMethod]
    public void TestJSC() => ReadJSC(JsonDataComment);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalid() => ReadJSC(JsonDataCommentInvalid);
    [TestMethod]
    public void TestEmptyValues()
    {
        string[] datas = [JsonDataEmptyObject, JsonDataEmptyArray, JsonDataRootString, JsonDataRootNumber, JsonDataRootBool, JsonDataRootNull];
        var sb = new StringBuilder(64);

        for (int i = 0; i < datas.Length; i++)
        {
            string data = datas[i];
            Read(sb, data);

            // Since the empty builder for the sample code is simple but somewhat matching, it should equal.
            Assert.AreEqual(data, sb.ToString());
        }
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestEmptyString()
    {
        Read(DataEmpty);
        Assert.Fail("Should have thrown exception for invalid data.");
    }

    /// <summary>
    /// This method must throw <see cref="SJReader.ReadException"/>
    /// </summary>
    /// <param name="tks">This argument must be the opening and enclosing tags for a recursive data structure.</param>
    /// <exception cref="ArgumentException"></exception>
    static void TestMaxRecursion(string tks)
    {
        if (string.IsNullOrEmpty(tks))
        {
            throw new ArgumentException($"'{nameof(tks)}' cannot be null or empty.", nameof(tks));
        }
        if (tks.Length != 2)
        {
            throw new ArgumentException($"'{nameof(tks)}' must exactly have two characters, one for opening and one for closing.", nameof(tks));
        }

        var reader = new SJStringReader();

        // If this is "ever changed" to less. 128 stack frames seem reasonable.
        reader.maxDepth = Math.Max(128, reader.maxDepth);
        reader.Data = string.Concat(string.Join("", Enumerable.Repeat(tks[0], reader.maxDepth + 1)), string.Join("", Enumerable.Repeat(tks[1], reader.maxDepth + 1)));

        Read(reader);
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxArrayRecursion() => TestMaxRecursion("[]");
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxObjectRecursion() => TestMaxRecursion("{}");

    // "valid.json" is from some random website.
    // Other JSON data sets are from https://microsoftedge.github.io/Demos/json-dummy-data/
    static void TestFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException($"'{nameof(fileName)}' cannot be null or empty.", nameof(fileName));
        }

        Assert.IsTrue(File.Exists(fileName), $"Test file not found at: {fileName}, please include with the test project.");
        Read(File.ReadAllText(fileName));
    }
    /// <summary>
    /// Tests reading a basic JSON file (100kb-ish).
    /// </summary>
    [TestMethod]
    public void TestFile() => TestFile(JsonFileValidName);
    /// <summary>
    /// Tests reading large JSON file.
    /// </summary>
    [TestMethod]
    [Timeout(30000)] // ← Change this if your PC is slower, but it's unlikely you will need this.
    public void TestVeryLarge()
    {
        TestFile(JsonFileLargeName);
        TestFile(JsonFileLargeMinName);
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidUnterminated() => TestFile(JsonFileInvalidUnterminated);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidNoColon() => TestFile(JsonFileInvalidNoColon);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidBinary() => TestFile(JsonFileInvalidBinary); // This could also throw IO error, but it don't.
}
