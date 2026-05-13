using System.Globalization;

namespace SJ.Tests;

[TestClass]
public sealed class WriterUnitTests
{
    static void WriteTestValues(SJWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (writer.Depth <= 0)
        {
            throw new ArgumentException("Can't write test values to top level object.", nameof(writer));
        }

        switch (writer.Top.type)
        {
            case SJType.Object:
                {
                    // There isn't any checks for duplicate keys.. Be careful!
                    if (writer.NeedValue)
                    {
                        Console.WriteLine($"The writer {writer} was expecting a value. Setting that key null.");
                        writer.WriteNull();
                    }

                    writer.WriteKey("yes");
                    writer.Write(true);

                    writer.WriteKey("no");
                    writer.Write(false);

                    writer.WriteKey("maybe");
                    writer.WriteNull();

                    writer.WriteKey("strings");
                    writer.Write("Did you know : Every minute in here, 60 seconds pass. But when I'm programming, every key press makes minute pass in twice the speed!");

                    writer.WriteKey("integer number");
                    writer.Write(12345 + 67740);

                    writer.WriteKey("float number");
                    writer.Write(Math.Sin(Math.PI * 0.245) * 100);

                    writer.WriteKey("array");
                    using (writer.Array())
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            writer.Write(i + 1);
                        }
                    }

                    writer.WriteKey("object");
                    using (writer.Object())
                    {
                        writer.WriteKey("uhhh");
                        writer.Write("idk..");
                    }
                    break;
                }
            case SJType.Array:
                {
                    if (writer.NeedValue)
                    {
                        Console.WriteLine($"The writer {writer} was expecting a value. Setting that key null.");
                        writer.WriteNull();
                    }
                    writer.Write(true);
                    writer.Write(false);
                    writer.WriteNull();
                    writer.Write("Did you know : Every minute in here, 60 seconds pass.");
                    writer.Write(12345 + 67740);
                    writer.Write(Math.Sin(Math.PI * 0.245) * 100);

                    using (writer.Array())
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            writer.Write(i + 1);
                        }
                    }

                    using (writer.Object())
                    {
                        writer.WriteKey("uhhh");
                        writer.Write("idk..");
                    }
                    break;
                }
            default:
                throw new ArgumentException($"Unexpected top level type {writer.Top.type}", nameof(writer));
        }
    }
    [TestMethod]
    public void TestRootWrite()
    {
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = true
        };

        // Root level writes
        writer.Write(true);
        Assert.AreEqual("true", writer.ToString());
        writer.Reset();

        writer.Write(false);
        Assert.AreEqual("false", writer.ToString());
        writer.Reset();

        writer.WriteNull();
        Assert.AreEqual("null", writer.ToString());
        writer.Reset();

        writer.Write(null);
        Assert.AreEqual("null", writer.ToString());
        writer.Reset();

        const string IntFmt = "G";
        const string FloatFmt = "R";
        var numberCulture = CultureInfo.InvariantCulture;

        const int IntVal = 676767;
        writer.Write(IntVal, IntFmt);
        Assert.AreEqual(IntVal.ToString(IntFmt), writer.ToString());
        writer.Reset();

        const long LongVal = 1234123412341234123L;
        writer.Write(LongVal, IntFmt);
        Assert.AreEqual(LongVal.ToString(IntFmt, numberCulture), writer.ToString());
        writer.Reset();

        const ulong ULongVal = 12341234123412341234UL;
        writer.Write(ULongVal, IntFmt);
        Assert.AreEqual(ULongVal.ToString(IntFmt, numberCulture), writer.ToString());
        writer.Reset();

        // Floating point numbers are somewhat indeterminate, so use the same format and hope for the best.
        const float FloatVal = 1.234f;
        writer.Write(FloatVal, FloatFmt);
        Assert.AreEqual(((double)FloatVal).ToString(FloatFmt, numberCulture), writer.ToString());
        writer.Reset();

        const double DoubleVal = 0.1d + 0.2d;
        writer.Write(DoubleVal, FloatFmt);
        Assert.AreEqual(DoubleVal.ToString(FloatFmt, numberCulture), writer.ToString());
        writer.Reset();

        // Generally, the test content isn't escaped (it's written as is)
        // The escaping is "tested", so I will not test that.
        const string TestContent = "Quote: \", Backslash: \\, Tab: \t, Newline: \n, Pizza: \uD83C\uDF55, The 🅱 variant: \uD83C\uDD71\uFE0F";
        writer.Write(TestContent);
        Assert.AreEqual($"\"{SJEscape.Escape(TestContent)}\"", writer.ToString());
        writer.Reset();
    }
    [TestMethod]
    public void TestWrite()
    {
        // Generic object write / read
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = true
        };

        // Writing an object that looks like this.
        using (writer.Object())
        {
            WriteTestValues(writer);
        }

        // Read resulting data
        string data = writer.ToString();
        // Validate
        ReaderUnitTests.Read(data);
        // And show
        Console.WriteLine(data);
    }

    static bool TestDepthWith(SJWriter writer)
    {
        writer.maxDepth = Math.Max(writer.maxDepth, 128);

        for (int i = 0; i < writer.maxDepth + 1; i++)
        {
            if (!writer.BeginObject())
            {
                return true;
            }
        }

        return false;
    }

    [TestMethod]
    [ExpectedException(typeof(SJWriter.WriteException))]
    public void TestDepth()
    {
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = true
        };

        Assert.IsTrue(TestDepthWith(writer), "Depth test must fail"); // ← Must throw WriteException instead
        Console.WriteLine($"fail : {writer}");
    }
    [TestMethod]
    public void TestDepthNoExcept()
    {
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = false
        };

        Assert.IsTrue(TestDepthWith(writer), "Depth test must fail");
        Assert.That.IsNotNullOrEmpty(writer.Error, "Writer should have an error set after failing");
    }

    static bool TestStackWith(SJWriter writer)
    {
        writer.BeginObject();

        writer.WriteKey("Fun fact : [redacted]!!");

        writer.BeginArray();
        bool result = !writer.EndObject(); // whoops
        result = result || !writer.EndArray();

        return result;
    }

    [TestMethod]
    [ExpectedException(typeof(SJWriter.WriteException))]
    public void TestStack()
    {
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = true
        };

        Assert.IsTrue(TestStackWith(writer), "Stack test must fail");
        Console.WriteLine($"fail : {writer}");
    }
    [TestMethod]
    public void TestStackNoExcept()
    {
        var writer = new SJStringBuilderWriter()
        {
            indentSize = 4,
            ThrowOnError = false
        };

        Assert.IsTrue(TestStackWith(writer), "Stack test must fail");
        Assert.That.IsNotNullOrEmpty(writer.Error, "Writer should have an error set after failing");
    }
}
