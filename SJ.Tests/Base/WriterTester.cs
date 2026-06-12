using System.Globalization;

namespace SJ.Tests;

public static class WriterTester
{
    public static bool WriteTest(SJWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (writer.depth <= 0)
        {
            throw new ArgumentException("Can't write test values to top level object.", nameof(writer));
        }

        int startingIndex = writer.PeekState().index;
        void CheckWriterIndex() => Assert.AreEqual(++startingIndex, writer.PeekState().index, "Expected write to increase top level element counter index");

        bool success = string.IsNullOrEmpty(writer.Error);
        switch (writer.PeekState().type)
        {
            case SJType.Object:
                {
                    // There isn't any checks for duplicate keys.. Be careful!
                    if ((writer.expect & SJWriter.Expect.Value) == SJWriter.Expect.Value)
                    {
                        Console.WriteLine($"The writer {writer} was expecting a value. Setting that key null.");
                        success = success && writer.WriteNull();
                    }

                    // "Index" count is tracked only on the top level recursive structures
                    success = success && writer.WriteKey("yes");
                    success = success && writer.Write(true);
                    CheckWriterIndex();

                    success = success && writer.WriteKey("no");
                    success = success && writer.Write(false);
                    CheckWriterIndex();

                    success = success && writer.WriteKey("maybe");
                    success = success && writer.WriteNull();
                    CheckWriterIndex();

                    success = success && writer.WriteKey("strings");
                    success = success && writer.Write("Did you know : Every minute in here 60 seconds pass. But when I'm programming every key press makes minute pass in twice the speed!");
                    CheckWriterIndex();

                    success = success && writer.WriteKey("integer number");
                    success = success && writer.Write(12345 + 67740);
                    CheckWriterIndex();

                    success = success && writer.WriteKey("float number");
                    success = success && writer.Write(Math.Sin(Math.PI * 0.245) * 100);
                    CheckWriterIndex();

                    // Use WriteKV
                    success = success && writer.WriteKV("yes 2", true);
                    CheckWriterIndex();
                    success = success && writer.WriteKV("no 2", false);
                    CheckWriterIndex();
                    success = success && writer.WriteKV("maybe 2", null);
                    CheckWriterIndex();
                    success = success && writer.WriteKV("strings 2", "bla bla bla");
                    CheckWriterIndex();
                    success = success && writer.WriteKV("calc short for calc", 50 + 3 - 7 + 12 - 5 + 24 - 12 + 53 + -12 - 40 + 1);
                    CheckWriterIndex();
                    success = success && writer.WriteKV("some number", Math.ScaleB(2.5, 4));
                    CheckWriterIndex();

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
                    if ((writer.expect & SJWriter.Expect.Value) == SJWriter.Expect.Value)
                    {
                        Console.WriteLine(
                            $"The writer {writer} was expecting a value." +
                            $"Setting that key null. Wait a minute"
                        );
                        Assert.Fail("NeedValue is true while supposedly on an array.");
                    }

                    success = success && writer.Write(true);
                    CheckWriterIndex();
                    success = success && writer.Write(false);
                    CheckWriterIndex();
                    success = success && writer.WriteNull();
                    CheckWriterIndex();
                    success = success && writer.Write("Did you know : Every minute in here, 60 seconds pass.");
                    CheckWriterIndex();
                    success = success && writer.Write(12345 + 67740);
                    CheckWriterIndex();
                    success = success && writer.Write(Math.Sin(Math.PI * 0.245) * 100);
                    CheckWriterIndex();

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
                throw new ArgumentException($"Unexpected top level type {writer.PeekState().type}", nameof(writer));
        }

        return success;
    }
    public static void WriteMustFailAfter(SJWriter writer)
    {
        Assert.That.IsNullOrEmpty(writer.Error, $"Writer error must be empty before checking a failing write. Error : {writer.Error}");
        Assert.IsFalse(writer.Write("Real or fake? No no fake!"), "Writing must fail");
    }
    public static bool WriteMaxTestDepth(SJWriter writer)
    {
        writer.maxDepth = Math.Max(writer.maxDepth, 128);
        return WriteTestDepth(writer, writer.maxDepth + 1);
    }
    public static bool WriteTestDepth(SJWriter writer, int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            if ((i % 2) == 0)
            {
                if (!writer.BeginObject()) return true;
            }
            else
            {
                // Previous entry was Object, which should be "valid" if it didn't fail
                if (!writer.WriteKey("a")) return false;
                if (!writer.BeginArray()) return true;
            }
        }

        return false;
    }
    public static void WriteTestRoot(SJWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        void WriterDataEquals(string s)
        {
            if (!writer.CanReadData)
            {
                Assert.Inconclusive($"[!] Cannot TestRootWith on writer type '{writer.GetType().AssemblyQualifiedName}' as data can't be read. Comparison was for '{s}'");
            }

            Assert.AreEqual(s, writer.ReadData());
        }

        // Root level writes
        writer.Write(true);
        WriterDataEquals("true");
        WriteMustFailAfter(writer);
        writer.Reset();

        writer.Write(false);
        WriterDataEquals("false");
        WriteMustFailAfter(writer);
        writer.Reset();

        writer.WriteNull();
        WriterDataEquals("null");
        WriteMustFailAfter(writer);
        writer.Reset();

        writer.Write(null);
        WriterDataEquals("null");
        WriteMustFailAfter(writer);
        writer.Reset();

        const string IntFmt = "G";
        const string FloatFmt = "R";
        var numberCulture = CultureInfo.InvariantCulture;

        const int IntVal = 676767;
        writer.Write(IntVal, IntFmt);
        WriterDataEquals(IntVal.ToString(IntFmt));
        WriteMustFailAfter(writer);
        writer.Reset();

        const long LongVal = 1234123412341234123L;
        writer.Write(LongVal, IntFmt);
        WriterDataEquals(LongVal.ToString(IntFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        const ulong ULongVal = 12341234123412341234UL;
        writer.Write(ULongVal, IntFmt);
        WriterDataEquals(ULongVal.ToString(IntFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Floating point numbers are somewhat indeterminate, so use the same format and hope for the best.
        // Note : Writer used to write floats using double, but now it writes using float type (so that the rounding is same).
        //        So the ToString behaviour should be same.
        const float FloatVal = 1.234f;
        writer.Write(FloatVal, FloatFmt);
        WriterDataEquals(FloatVal.ToString(FloatFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        const double DoubleVal = 0.1d + 0.2d;
        writer.Write(DoubleVal, FloatFmt);
        WriterDataEquals(DoubleVal.ToString(FloatFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Well I didn't know this was possible
        const string DoubleFmt = "G999";
        writer.Write(double.MaxValue, DoubleFmt);
        WriterDataEquals(double.MaxValue.ToString(DoubleFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Generally, the test content isn't escaped (it's written as is)
        // The escaping is "tested", so I will not test that.
        const string TestContent = "Quote: \", Backslash: \\, Tab: \t, Newline: \n, Pizza: \uD83C\uDF55, The 🅱 variant: \uD83C\uDD71\uFE0F";
        writer.Write(TestContent);
        WriterDataEquals($"\"{SJEscape.Escape(TestContent)}\"");
        WriteMustFailAfter(writer);
        writer.Reset();
    }
    public static void WriteTestRootJSC(SJWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        void WriterDataEquals(string s)
        {
            if (!writer.CanReadData)
            {
                Assert.Inconclusive($"[!] Cannot TestRootWith on writer type '{writer.GetType().AssemblyQualifiedName}' as data can't be read. Comparison was for '{s}'");
            }

            Assert.AreEqual(s, writer.ReadData());
        }

        writer.indentSize = 4;
        writer.allowComments = true;

        const char Pad = ' ';
        const string C1 = "CommentLine", C2 = "Comment ML", C3 = "Last Comment Line"; 
        // Root level writes
        writer.WriteCommentLine(C1, Pad);
        writer.Write(true);
        writer.WriteComment(C2, Pad);
        writer.WriteCommentLine(C3, Pad);
        WriterDataEquals($"//{Pad}{C1}\ntrue/*{Pad}{C2}{Pad}*/\n//{Pad}{C3}");
        WriteMustFailAfter(writer);
        writer.Reset();

        /*
        writer.Write(false);
        WriterDataEquals("false");
        WriteMustFailAfter(writer);
        writer.Reset();

        writer.WriteNull();
        WriterDataEquals("null");
        WriteMustFailAfter(writer);
        writer.Reset();

        writer.Write(null);
        WriterDataEquals("null");
        WriteMustFailAfter(writer);
        writer.Reset();

        const string IntFmt = "G";
        const string FloatFmt = "R";
        var numberCulture = CultureInfo.InvariantCulture;

        const int IntVal = 676767;
        writer.Write(IntVal, IntFmt);
        WriterDataEquals(IntVal.ToString(IntFmt));
        WriteMustFailAfter(writer);
        writer.Reset();

        const long LongVal = 1234123412341234123L;
        writer.Write(LongVal, IntFmt);
        WriterDataEquals(LongVal.ToString(IntFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        const ulong ULongVal = 12341234123412341234UL;
        writer.Write(ULongVal, IntFmt);
        WriterDataEquals(ULongVal.ToString(IntFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Floating point numbers are somewhat indeterminate, so use the same format and hope for the best.
        // Note : Writer used to write floats using double, but now it writes using float type (so that the rounding is same).
        //        So the ToString behaviour should be same.
        const float FloatVal = 1.234f;
        writer.Write(FloatVal, FloatFmt);
        WriterDataEquals(FloatVal.ToString(FloatFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        const double DoubleVal = 0.1d + 0.2d;
        writer.Write(DoubleVal, FloatFmt);
        WriterDataEquals(DoubleVal.ToString(FloatFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Well I didn't know this was possible
        const string DoubleFmt = "G999";
        writer.Write(double.MaxValue, DoubleFmt);
        WriterDataEquals(double.MaxValue.ToString(DoubleFmt, numberCulture));
        WriteMustFailAfter(writer);
        writer.Reset();

        // Generally, the test content isn't escaped (it's written as is)
        // The escaping is "tested", so I will not test that.
        const string TestContent = "Quote: \", Backslash: \\, Tab: \t, Newline: \n, Pizza: \uD83C\uDF55, The 🅱 variant: \uD83C\uDD71\uFE0F";
        writer.Write(TestContent);
        WriterDataEquals($"\"{SJEscape.Escape(TestContent)}\"");
        WriteMustFailAfter(writer);
        writer.Reset();
        */
    }
}
