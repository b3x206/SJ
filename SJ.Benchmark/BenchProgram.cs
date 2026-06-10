using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using System.Text;

namespace SJ.Benchmark
{
    [MemoryDiagnoser]
    public class SJBenchmark
    {
        const int NIters = 256;
        const int NItersLarge = 16;
        static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string JsonFileValidName = Path.Combine(BaseDirectory, "TestFiles", "valid.json");
        static readonly string JsonFileLargeName = Path.Combine(BaseDirectory, "TestFiles", "5mb.json");
        string validText = string.Empty;
        string largeText = string.Empty;

        const int NWriteElem = 512;
        const int NWriteLargeElem = 16384;
        const int WriteKeySize = 8, WriteValueSize = 16;
        const int WriteIntoArrayEveryN = 16; // 16 KV, 1 K 16 Array entries
        StringBuilder? sharedBuilder;

        [GlobalSetup]
        public void Setup()
        {
            validText = File.ReadAllText(JsonFileValidName);
            largeText = File.ReadAllText(JsonFileLargeName);
            sharedBuilder = new StringBuilder(524288); // Accomodating for the largest capacity in the writer.
        }

        // Read
        static void ReadRecursive(SJReader reader, ref long counter) => ReadRecursiveInternal(reader, reader.Read(), ref counter);
        static void ReadRecursiveInternal(SJReader reader, SJReader.Value value, ref long counter)
        {
            switch (value.type)
            {
                case SJType.Comment:
                case SJType.Number:
                case SJType.String:
                case SJType.Bool:
                case SJType.Null:
                    unchecked { counter += value.Slice().Length; }
                    break;

                case SJType.Object:
                    SJReader.Value k = SJReader.Value.Error();
                    while (reader.IterateObjectEntries(value, out var t, out var v))
                    {
                        switch (t)
                        {
                            default:
                                ReadRecursiveInternal(reader, v, ref counter);
                                break;

                            case SJReader.EntryType.Key:
                                k = v;
                                break;
                            case SJReader.EntryType.Value:
                                ReadRecursiveInternal(reader, k, ref counter);
                                counter += 1;
                                ReadRecursiveInternal(reader, v, ref counter);
                                break;
                        }
                    }
                    break;
                case SJType.Array:
                    while (reader.IterateArray(value, out var v))
                    {
                        ReadRecursiveInternal(reader, v, ref counter);
                    }
                    break;

                default:
                    throw new Exception($"Unexpected type or error : {reader.Error}");
            }
        }
        static long BenchRead(string text, int count)
        {
            long counter = 0;
            var reader = new SJStringReader(text);
            for (int i = 0; i < count; i++)
            {
                ReadRecursive(reader, ref counter);
                reader.Reset();
            }

            return counter;
        }
        [Benchmark]
        public long Read() => BenchRead(validText, NIters);
        [Benchmark]
        public long ReadLarge() => BenchRead(largeText, NItersLarge);

        // Write
        static readonly Random rand = new();
        static void RandomString(ref Span<char> result)
        {
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz012345678901";
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Chars[rand.Next(Chars.Length)];
            }
        }
        static int BenchWrite(SJWriter writer, int iters, int elemCount)
        {
            Span<char> key = stackalloc char[WriteKeySize];
            Span<char> value = stackalloc char[WriteValueSize];
            writer.Reset();

            for (int i = 0; i < iters; i++)
            {
                writer.Reset();

                using (writer.Object())
                {
                    for (int j = 0; j < elemCount; j++)
                    {
                        RandomString(ref key);
                        RandomString(ref value);
                        writer.WriteKV(key, value);

                        if (j > 0 && (j % WriteIntoArrayEveryN) == 0)
                        {
                            RandomString(ref key);
                            using (writer.ArrayKV(key))
                            {
                                for (int k = 0; k < WriteIntoArrayEveryN && k + j < elemCount; k++, j++)
                                {
                                    RandomString(ref value);
                                    writer.WriteString(value);
                                }
                            }
                        }
                    }
                }
            }

            return writer.count;
        }
        // Length (with default settings) = 15857 for normal, 507559 for large
        [Benchmark]
        public int Write()
        {
            sharedBuilder!.Clear();
            return BenchWrite(new SJStringWriter(sharedBuilder), NIters, NWriteElem);
        }
        [Benchmark]
        public int WriteLarge()
        {
            sharedBuilder!.Clear();
            return BenchWrite(new SJStringWriter(sharedBuilder), NItersLarge, NWriteLargeElem);
        }
    }

    sealed class BenchProgram
    {
        static void Main()
        {
            var config = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Microsecond));
            BenchmarkRunner.Run<SJBenchmark>(config);
        }
    }
}
