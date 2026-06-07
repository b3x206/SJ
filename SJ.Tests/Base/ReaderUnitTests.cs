using static SJ.Tests.TestData;
using static SJ.Tests.ReaderTester;

namespace SJ.Tests;

/// <summary>
/// Base unit tests for a <see cref="SJReader"/> that must pass.
/// </summary>
/// <typeparam name="TReader">Type for the target <see cref="SJReader"/></typeparam>
public abstract class ReaderUnitTests<TReader> where TReader : SJReader
{
    // Config
    /// <summary>
    /// Create <typeparamref name="TReader"/> with the data for <paramref name="data"/>.
    /// </summary>
    public abstract TReader CreateWithString(string data);
    /// <summary>
    /// Create <typeparamref name="TReader"/> with the file data located at <paramref name="path"/>.
    /// </summary>
    public abstract TReader CreateWithFile(string path);
    /// <summary>
    /// Create a string that will exceed <paramref name="maxDepth"/>.
    /// </summary>
    /// <param name="tks">This argument must be the opening and enclosing tags for a recursive data structure, making it exactly 2 chars.</param>
    /// <exception cref="ArgumentException"></exception>
    protected TReader CreateWithMaxRecursionString(string tks, int maxDepth)
    {
        if (string.IsNullOrEmpty(tks))
        {
            throw new ArgumentException($"'{nameof(tks)}' cannot be null or empty.", nameof(tks));
        }
        if (tks.Length != 2)
        {
            throw new ArgumentException($"'{nameof(tks)}' must exactly have two characters, one for opening and one for closing.", nameof(tks));
        }

        return CreateWithString(string.Concat(string.Join("", Enumerable.Repeat(tks[0], maxDepth + 1)), string.Join("", Enumerable.Repeat(tks[1], maxDepth + 1))));
    }
    /// <summary>
    /// Depth of property used on "MaxNRecursion". Override if your class initializes this property differently on a constructor.
    /// </summary>
    public virtual int MaxRecursionDepth => 128;

    // Tests
    // Basic tests: Read and Empty
    [TestMethod]
    public void TestBasic() => Read(CreateWithString(JsonData1));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestBasicInvalid() => Read(CreateWithString(JsonDataInvalid));
    [TestMethod]
    public void TestSlightlyComplicated() => Read(CreateWithString(JsonData2));
    [TestMethod]
    public void TestEmojiSpam()
    {
        var reader = CreateWithString(JsonData3);
        var root = reader.Read();
        Assert.AreEqual(root.type, SJType.Object, $"Expected root to be object, it is instead this : {root}");
        while (reader.IterateObject(root, out var k, out var v))
        {
            Assert.AreEqual(new string(k.Slice()), JsonDataSpamKey, "Keys must be equal");
            Assert.AreEqual(k.type, SJType.String);
            Assert.AreEqual(new string(v.Slice()), JsonDataSpamValue, "Values must be equal");
            Assert.AreEqual(v.type, SJType.String);
        }
    }

    // Discarding
    protected static void TestDiscard(SJReader reader, string discardKey = JsonDataDiscardKey)
    {
        // Manual reading
        ArgumentNullException.ThrowIfNull(reader);
        reader.ThrowOnError = true;

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
    public void TestDiscard() => TestDiscard(CreateWithString(JsonDataDiscard));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestDiscardInvalid() => TestDiscard(CreateWithString(JsonDataDiscardInvalid));

    // JSC
    [TestMethod]
    public void TestJSC() => ReadJSC(CreateWithString(JsonDataComment));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalid() => ReadJSC(CreateWithString(JsonDataCommentInvalid));

    // Empty root
    [TestMethod]
    public void TestEmptyValues()
    {
        string[] datas = [JsonDataEmptyObject, JsonDataEmptyArray, JsonDataRootString, JsonDataRootNumber, JsonDataRootBool, JsonDataRootNull];
        for (int i = 0; i < datas.Length; i++)
        {
            string data = datas[i];
            
            // Since the empty builder for the sample code is simple but somewhat matching, it should equal.
            Assert.AreEqual(data, Read(CreateWithString(data)).ToString());
        }
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestEmptyString() => Read(CreateWithString(DataEmpty));

    // Stack
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestStacking() => Read(CreateWithString(JsonDataStackingInvalid));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxArrayRecursion() => Read((CreateWithMaxRecursionString("[]", MaxRecursionDepth)));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxObjectRecursion() => Read(CreateWithMaxRecursionString("{}", MaxRecursionDepth));

    // - File tests
    /// <summary>
    /// Tests reading a basic JSON file (100kb-ish).
    /// </summary>
    [TestMethod]
    public void TestFile() => Read(CreateWithFile(JsonFileValidName));
    /// <summary>
    /// Tests reading large JSON file.
    /// </summary>
    [TestMethod]
    [Timeout(30000)] // ← Change this if your PC is slower, but it's unlikely you will need this.
    public void TestVeryLarge()
    {
        Read(CreateWithFile(JsonFileLargeName));
        Read(CreateWithFile(JsonFileLargeMinName));
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidUnterminated() => Read(CreateWithFile(JsonFileInvalidUnterminated));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidNoColon() => Read(CreateWithFile(JsonFileInvalidNoColon));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidBinary() => Read(CreateWithFile(JsonFileInvalidBinary));
}
