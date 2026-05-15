using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

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
        static readonly string[] Values = [
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
            "Cras facilisis odio vel vehicula porta.",
            "Vestibulum at lorem convallis, imperdiet nisl sit amet, blandit quam.",

            "Maecenas viverra dui vel egestas porta.",
            "Vestibulum aliquet lacus non neque placerat sollicitudin.",
            "Nam tempus nibh sit amet ultricies vestibulum.",
            "Nulla malesuada lectus ac urna varius iaculis.",
            "Pellentesque facilisis nisi sollicitudin dui efficitur venenatis.",

            "Morbi fringilla ante finibus magna bibendum, in varius justo aliquam.",
            "Fusce et erat dignissim, fringilla libero maximus, luctus est.",
            "Phasellus eget mi sagittis, mollis diam non, mollis tellus.",
            "Vivamus nec mauris eget nisi mattis lacinia.",

            "Donec vitae justo in magna rutrum pellentesque.",
            "Ut nec tellus nec dui lacinia pharetra.",

            "Vestibulum viverra purus id sem varius, ut porta metus ullamcorper.",
            "Ut a dolor placerat, eleifend leo eget, semper sem.",
        ];

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
            Span<char> key = stackalloc char[16];

            for (int i = 0; i < NWrite; i++)
            {
                using (writer.Object())
                {
                    for (int j = 0; j < elemCount; j++)
                    {
                        RandomString(ref key);
                        writer.WriteKV(key, Values[j % Values.Length]);

                        // when will i learn.. it's cleaner to start a "sub loop" rather than to defer behaviour
                        // while there isn't any clean "defer" built into the language.
                        if (j > 0 && (j % WriteIntoArrayEveryN) == 0)
                        {
                            for (int k = 0; k < WriteIntoArrayEveryN && (k + j) < elemCount; k++, j++)
                            {
                                using (writer.Array())
                                {
                                    writer.WriteString(Values[j % Values.Length]);
                                }
                            }
                        }
                    }
                }
            }

            // To not discard the processed value
            return writer;
        }
        [Benchmark]
        public SJWriter Write() => BenchWrite(new SJStringWriter(), NWriteElem);
        [Benchmark]
        public SJWriter WriteLarge() => BenchWrite(new SJStringWriter(), NWriteLargeElem);
    }

    sealed class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SJBenchmark>();
            Console.WriteLine(summary);
        }
    }
}
