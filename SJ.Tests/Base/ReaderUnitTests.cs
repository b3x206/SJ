using static SJ.Tests.TestData;
using static SJ.Tests.ReaderTester;
using System.Text;
using System.Reflection;

namespace SJ.Tests;

/// <summary>
/// Base unit tests for a <see cref="SJReader"/> that must pass.
/// </summary>
/// <typeparam name="TReader">Type for the target <see cref="SJReader"/></typeparam>
public abstract class ReaderUnitTests<TReader> where TReader : SJReader
{
    // uh oh:
    // * https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#avoid-coding-logic-in-unit-tests
    // anyways, "it works"(tm). if only the data attributes could have been extended better.

    // Config
    public const int SmallTestTimeout = 1000, MidTestTimeout = 5000, LargeTestTimeout = 30000;
    /// <summary>
    /// Create <typeparamref name="TReader"/> with the data for <paramref name="data"/>.
    /// </summary>
    public abstract TReader CreateWithString(string data);
    /// <summary>
    /// Create <typeparamref name="TReader"/> with encoded data, if encoding related matters are to be tested. (for StringReader, this just tests the System libraries, but it will make sense for buffered readers)
    /// </summary>
    /// <param name="data">
    /// Data stream, can be created by a stream or something else.
    /// The stream can be disposed by this method if the <typeparamref name="TReader"/> does not rely on it.
    /// </param>
    /// <param name="enc">Encoding of the stream, if applicable.</param>
    public abstract TReader CreateWithStream(Stream data, Encoding? enc);
    /// <summary>
    /// Create <typeparamref name="TReader"/> with the file data located at <paramref name="path"/>.
    /// </summary>
    public virtual TReader CreateWithFile(string path)
    {
        Assert.IsTrue(File.Exists(path), $"File must exist in path '{path}'");
        var fs = File.OpenRead(path);
        return CreateWithStream(fs, null);
    }
    /// <summary>
    /// Re-encodes <paramref name="data"/> to a stream and feeds it into <see cref="CreateWithStream(Stream)"/>.
    /// </summary>
    /// <param name="data">Data to create from.</param>
    /// <param name="enc">Encoding to mutilate <paramref name="data"/>'s bits.</param>
    public virtual TReader CreateWithEncodedString(string data, Encoding enc)
    {
        var ms = new MemoryStream(enc.GetBytes(data));
        return CreateWithStream(ms, enc);
    }
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

        if (tks[0] == '{')
        {
            // keys are needed for object
            // {"a":{"a":...
            return CreateWithString(string.Concat(tks[0], string.Join("", Enumerable.Repeat($"\"a\":{tks[0]}", maxDepth)), string.Join("", Enumerable.Repeat(tks[1], maxDepth + 1))));
        }
        else
        {
            return CreateWithString(string.Concat(string.Join("", Enumerable.Repeat(tks[0], maxDepth + 1)), string.Join("", Enumerable.Repeat(tks[1], maxDepth + 1))));
        }
    }
    /// <summary>
    /// Depth of property used on "MaxNRecursion". Override if your class initializes this property differently on a constructor.
    /// </summary>
    public virtual int MaxRecursionDepth => 128;
    /// <summary>
    /// Processors for the encoding parameter in the tests.
    /// </summary>
    public static IEnumerable<object[]> EncodedStringProcessors =>
        [[Encoding.UTF8], [Encoding.Unicode], [Encoding.BigEndianUnicode], [Encoding.UTF32]];
    public static string GetEncodedTestName(MethodInfo info, object[] data)
    {
        var data0 = data[0];
        if (ReferenceEquals(data0, Encoding.Unicode))
        {
            return $"{info.Name}_UnicodeEncoding";
        }
        if (ReferenceEquals(data0, Encoding.BigEndianUnicode))
        {
            return $"{info.Name}_BigEndianUnicodeEncoding";
        }
        return $"{info.Name}_{data0}";
    }
    /// <summary>
    /// Collection of root data (that are either empty or not)
    /// </summary>
    public static IEnumerable<object[]> JsonRootDataProcessors => [[JsonDataEmptyObject], [JsonDataEmptyArray], [JsonDataRootString], [JsonDataRootNumber], [JsonDataRootBool], [JsonDataRootNull]];
    public static string GetRootDataProcessorTestName(MethodInfo info, object[] data) => $"{info.Name}_{data[0]}";

    // Tests
    // Basic tests: Read and Empty
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    public void TestBasic() => Read(CreateWithString(JsonData1));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    [Timeout(SmallTestTimeout)]
    public void TestBasicInvalid() => Read(CreateWithString(JsonDataInvalid));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    public void TestSlightlyComplicated() => Read(CreateWithString(JsonData2));
    // Emoji spams will get differently encoded strings
    [TestMethod]
    [Timeout(MidTestTimeout)]
    [DynamicData(nameof(EncodedStringProcessors), DynamicDataDisplayName = nameof(GetEncodedTestName))]
    public void TestEmojiSpam(Encoding enc)
    {
        var reader = CreateWithEncodedString(JsonData3, enc);
        var root = reader.Read();
        Assert.AreEqual(SJType.Object, root.type, $"Expected root to be object, it is instead this : {root}");
        while (reader.IterateObject(root, out var k, out var v))
        {
            Assert.AreEqual(new string(k.Slice()), JsonDataSpamKey, "Keys must be equal");
            Assert.AreEqual(SJType.String, k.type);
            Assert.AreEqual(new string(v.Slice()), JsonDataSpamValue, "Values must be equal");
            Assert.AreEqual(SJType.String, v.type);
        }
    }
    [TestMethod]
    [Timeout(MidTestTimeout)]
    [DynamicData(nameof(EncodedStringProcessors), DynamicDataDisplayName = nameof(GetEncodedTestName))]
    public void TestEmojiSpamLiteral(Encoding enc)
    {
        // Buffered readers can struggle with this with invalidly truncated read from the resources with other encodings.
        var reader = CreateWithEncodedString($"\"{DataEmojiSpam}\"", enc);
        var root = reader.Read();
        Assert.AreEqual(SJType.String, root.type);
        Assert.IsTrue(root.Slice().SequenceEqual(DataEmojiSpam), "Sliced value must equal the string inside the literal.");
    }
    // Check tricky conditions on the comma/colon state
    [TestMethod]
    [DataRow(64)]
    [Timeout(SmallTestTimeout)]
    public void TestValidDeepObjects(int depth) => Read(CreateWithMaxRecursionString("{}", depth));
    [TestMethod]
    [DataRow(64)]
    [Timeout(SmallTestTimeout)]
    public void TestValidDeepArray(int depth) => Read(CreateWithMaxRecursionString("[]", depth));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [DataRow(@"[""hello"" 1 2 3 4 5]")]
    [DataRow(@"{""hello"": 124 ""world!"": ""world: yo"" ""look"": ""no comma! waow bradar please JSON code make no mistakes""}")]
    [DataRow(@"{""hello"" 124, ""gurt"" ""yo"", ""yogurt"" ""yes""}")]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestCommaColonState(string data) => Read(CreateWithString(data));

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
                    while (reader.IterateEntries(v, out var av))
                    {
                        Console.WriteLine($"  {av.Slice()}");
                    }
                    break;
            }
        }
    }
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    public void TestDiscard() => TestDiscard(CreateWithString(JsonDataDiscard));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestDiscardInvalid() => TestDiscard(CreateWithString(JsonDataDiscardInvalid));

    // JSC
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    public void TestJSC() => ReadJSC(CreateWithString(JsonDataComment));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalid() => ReadJSC(CreateWithString(JsonDataCommentInvalid));

    // JSC with Capture (hell)
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    public void TestJSCWithCapture() => ReadJSC(CreateWithString(JsonDataComment), false);
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [DataRow(@"{ ""key"" /* inline */ : ""value"" }")]
    [DataRow(@"{ ""key"" : /* inline */ ""value"" }")]
    [DataRow(@"{ ""key"" : ""value"" /* trailing */ , ""key2"": ""value2"" }")]
    [DataRow(@"[ 1, 2, 3 /* trailing array */ , 4, 5]")]
    [DataRow(@"[ 1, 2, 3 /* trailing array */]")]
    public void TestJSCWithTrickyCapture(string data) => ReadJSC(CreateWithString(data), false);
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalidWithCapture() => ReadJSC(CreateWithString(JsonDataInvalid), false);
    // Do also read literals with no ignore JSC to test "Ended".
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [DynamicData(nameof(JsonRootDataProcessors), DynamicDataDisplayName = nameof(GetRootDataProcessorTestName))]
    public void TestJSCRootLiteralsWithCapture(string data)
    {
        // Micro$oft pls
        string CommentLeft = "// This is a comment!" + Environment.NewLine;
        string CommentRight = " // One more comment!" + Environment.NewLine + "/* Is it the end of file? */" + Environment.NewLine;

        Assert.AreEqual(data, ReadJSC(CreateWithString(data), false).ToString());
        // To spice it up, surround root data with comments on second iter.
        var data2 = $"{CommentLeft}{data}{CommentRight}";
        Assert.AreEqual(data2, ReadJSC(CreateWithString(data2), false).ToString());
    }

    // Empty root
    [TestMethod]
    [DynamicData(nameof(JsonRootDataProcessors), DynamicDataDisplayName = nameof(GetRootDataProcessorTestName))]
    public void TestRootLiterals(string data)
    {
        // Since the empty builder for the sample code is simple but somewhat matching, it should equal.
        Assert.AreEqual(data, Read(CreateWithString(data)).ToString());
    }
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestEmptyString() => Read(CreateWithString(DataEmpty));

    // Stack
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestStacking() => Read(CreateWithString(JsonDataStackingInvalid));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxArrayRecursion() => Read((CreateWithMaxRecursionString("[]", MaxRecursionDepth)));
    [TestMethod]
    [Timeout(SmallTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxObjectRecursion() => Read(CreateWithMaxRecursionString("{}", MaxRecursionDepth));

    // - File tests
    /// <summary>
    /// Tests reading a basic JSON file (100kb-ish).
    /// </summary>
    [TestMethod]
    [Timeout(MidTestTimeout)]
    public void TestFile() => Read(CreateWithFile(JsonFileValidName));
    /// <summary>
    /// Tests reading large JSON file.
    /// </summary>
    [TestMethod]
    [Timeout(LargeTestTimeout)]
    public void TestVeryLarge()
    {
        Read(CreateWithFile(JsonFileLargeName));
        Read(CreateWithFile(JsonFileLargeMinName));
    }
    [TestMethod]
    [Timeout(MidTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidUnterminated() => Read(CreateWithFile(JsonFileInvalidUnterminated));
    [TestMethod]
    [Timeout(MidTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidNoColon() => Read(CreateWithFile(JsonFileInvalidNoColon));
    [TestMethod]
    [Timeout(MidTestTimeout)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidBinary() => Read(CreateWithFile(JsonFileInvalidBinary));
}
