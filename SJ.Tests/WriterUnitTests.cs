using System.Globalization;

namespace SJ.Tests;

[TestClass]
public sealed class WriterUnitTests
{
    static bool WriteTestValues(SJWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (writer.Depth <= 0)
        {
            throw new ArgumentException("Can't write test values to top level object.", nameof(writer));
        }

        // golang core :
        bool success = string.IsNullOrEmpty(writer.Error);
        switch (writer.Top.type)
        {
            case SJType.Object:
                {
                    // There isn't any checks for duplicate keys.. Be careful!
                    if (writer.NeedValue)
                    {
                        Console.WriteLine($"The writer {writer} was expecting a value. Setting that key null.");
                        success = success && writer.WriteNull();
                    }

                    success = success && writer.WriteKey("yes");
                    success = success && writer.Write(true);

                    success = success && writer.WriteKey("no");
                    success = success && writer.Write(false);

                    success = success && writer.WriteKey("maybe");
                    success = success && writer.WriteNull();

                    success = success && writer.WriteKey("strings");
                    success = success && writer.Write("Did you know : Every minute in here 60 seconds pass. But when I'm programming every key press makes minute pass in twice the speed!");

                    success = success && writer.WriteKey("integer number");
                    success = success && writer.Write(12345 + 67740);

                    success = success && writer.WriteKey("float number");
                    success = success && writer.Write(Math.Sin(Math.PI * 0.245) * 100);

                    // Use WriteKV
                    success = success && writer.WriteKV("yes 2", true);
                    success = success && writer.WriteKV("no 2", false);
                    success = success && writer.WriteKV("maybe 2", null);
                    success = success && writer.WriteKV("strings 2", "bla bla bla");
                    success = success && writer.WriteKV("calc short for calc", 50 + 3 - 7 + 12 - 5 + 24 - 12 + 53 + -12 - 40 + 1);
                    success = success && writer.WriteKV("some number", Math.ScaleB(2.5, 4));

                    success = success && writer.WriteKey("array");
                    if (success)
                        using (writer.Array())
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                success = success && writer.Write(i + 1);
                            }
                        }
                    success = success && writer.WriteKey("object");
                    if (success)
                        using (writer.Object())
                        {
                            success = success && writer.WriteKey("uhhh");
                            success = success && writer.Write("idk..");
                        }
                    break;
                }
            case SJType.Array:
                {
                    if (writer.NeedValue)
                    {
                        Console.WriteLine(
                            $"The writer {writer} was expecting a value." +
                            $"Setting that key null. Wait a minute"
                        );
                        Assert.Fail("NeedValue is true while supposedly on an array.");
                    }

                    success = success && writer.Write(true);
                    success = success && writer.Write(false);
                    success = success && writer.WriteNull();
                    success = success && writer.Write("Did you know : Every minute in here, 60 seconds pass.");
                    success = success && writer.Write(12345 + 67740);
                    success = success && writer.Write(Math.Sin(Math.PI * 0.245) * 100);

                    if (success)
                        using (writer.Array())
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                success = success && writer.Write(i + 1);
                            }
                        }

                    if (success)
                        using (writer.Object())
                        {
                            success = success && writer.WriteKey("uhhh");
                            success = success && writer.Write("idk..");
                        }
                    break;
                }
            default:
                throw new ArgumentException($"Unexpected top level type {writer.Top.type}", nameof(writer));
        }

        return success;
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
        // Note : SJWriter used to write floats using double, but now it writes using float.
        //        So the ToString behaviour should be same.
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
            // Though ThrowOnError is true, so :shrug:
            Assert.IsTrue(WriteTestValues(writer), "Writing must go without any errors");

            using (writer.ArrayKV("test on le array"))
            {
                Assert.IsTrue(WriteTestValues(writer), "Writing must go without any errors");
            }
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
