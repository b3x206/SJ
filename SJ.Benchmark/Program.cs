using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Text;

namespace SJ.Benchmark
{
    [MemoryDiagnoser]
    public class SJBenchmark
    {
        const int NRead = 256;
        const int NReadLarge = 16;
        static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static readonly string JsonFileValidName = Path.Combine(BaseDirectory, "TestFiles", "valid.json"),
            JsonFileLargeName = Path.Combine(BaseDirectory, "TestFiles", "5mb.json");
        private string validText = string.Empty;
        private string largeText = string.Empty;

        // Dumps all KV into an array. Should count into the main counter.
        const int NWrite = 128;
        const int NWriteElem = 32;
        const int NWriteLargeElem = 256;
        const int WriteIntoArrayEveryN = 8;

        [GlobalSetup]
        public void Setup()
        {
            validText = File.ReadAllText(JsonFileValidName);
            largeText = File.ReadAllText(JsonFileLargeName);
        }

        static void ReadRecursive(SJReader reader, ref long counter) => ReadRecursiveInternal(reader, reader.Read(), ref counter);
        static void ReadRecursiveInternal(SJReader reader, SJReader.Value value, ref long counter)
        {
            switch (value.type)
            {
                case SJType.Number:
                    counter += value.Slice().Length;
                    break;
                case SJType.String:
                    counter += value.Slice().Length;
                    break;
                case SJType.Bool:
                    counter += value.Slice().Length;
                    break;
                case SJType.Null:
                    counter += value.Slice().Length;
                    break;
                case SJType.Object:
                    while (reader.IterateObject(value, out var k, out var v))
                    {
                        counter += k.Slice().Length;
                        ReadRecursiveInternal(reader, v, ref counter);
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
        public long Read() => BenchRead(validText, NRead);
        [Benchmark]
        public long ReadLarge() => BenchRead(largeText, NReadLarge);

        [ThreadStatic]
        static Random? rand;
        static void RandomString(ref Span<char> result)
        {
            rand ??= new Random();
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz012345678901";
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Chars[rand.Next(Chars.Length)];
            }
        }
        static SJWriter BenchWrite(SJWriter writer, int elemCount)
        {
            writer.Reset();
            Span<char> key = stackalloc char[8];
            Span<char> value = stackalloc char[8];

            for (int i = 0; i < NWrite; i++)
            {
                using (writer.Object())
                {
                    for (int j = 0; j < elemCount; j++)
                    {
                        RandomString(ref key);
                        RandomString(ref value);
                        writer.WriteKV(key, value);

                        // when will i learn.. it's cleaner to start a "sub loop" rather than to defer behaviour
                        // while there isn't any clean "defer" built into the language.
                        if (j > 0 && (j % WriteIntoArrayEveryN) == 0)
                        {
                            for (int k = 0; k < WriteIntoArrayEveryN && (k + j) < elemCount; k++, j++)
                            {
                                using (writer.Array())
                                {
                                    RandomString(ref value);
                                    writer.WriteString(value);
                                }
                            }
                        }
                    }
                }
            }

            // To not discard the processed value
            Console.WriteLine($"Writer count : {writer.count}, elemCount : {elemCount}");
            return writer;
        }
#pragma warning disable CA1822
        [Benchmark]
        public SJWriter WriteBasic()
        {
            // {"1":1,...}
            var writer = new SJStringWriter((NWriteElem * 5) + 8);
            Span<char> digitChar = stackalloc char[1];

            using (writer.Object())
            {
                for (int i = 0; i < NWriteElem; i++)
                {
                    int v = i % 10;
                    digitChar[0] = (char)(v + '0');
                    writer.WriteKV(digitChar, v);
                }
            }

            return writer;
        }
        [Benchmark]
        public SJWriter Write() => BenchWrite(new SJStringWriter(), NWriteElem);
        [Benchmark]
        public SJWriter WriteLarge() => BenchWrite(new SJStringWriter(), NWriteLargeElem);
#pragma warning restore CA1822
    }

    sealed class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SJBenchmark>();
        }
    }
}
