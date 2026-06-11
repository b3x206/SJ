using BenchmarkDotNet.Attributes;

namespace SJ.Benchmark
{
    public abstract class SJWriterBenchmark<TWriter> where TWriter : SJWriter
    {
        public const int NWriteIters = 256;
        public const int NWriteLargeIters = 32;
        public const int NWriteElem = 512;
        public const int NWriteLargeElem = 16384;
        public const int WriteIntoArrayEveryN = 16; // 16 KV, 1 K 16 Array entries

        public const int RandomKeySize = 8, RandomValueSize = 16;
        public const int RandomKeyStringsCount = 512, RandomValueStringsCount = 512;
        public static readonly string randomizedKeyStrings = string.Create(RandomKeySize * RandomKeyStringsCount, 0, (c, _) => RandomString(ref c)),
            randomizedValueStrings = string.Create(RandomValueSize * RandomKeyStringsCount, 0, (c, _) => RandomString(ref c));

        [ThreadStatic]
        private static Random? rand;
        public static void RandomString(ref Span<char> result)
        {
            rand ??= new();
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz012345678901";
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Chars[rand.Next(Chars.Length)];
            }
        }
        public static ReadOnlySpan<char> GetRandomKey(int n)
        {
            return randomizedKeyStrings.AsSpan((RandomKeySize * n) % randomizedKeyStrings.Length, RandomKeySize);
        }
        public static ReadOnlySpan<char> GetRandomValue(int n)
        {
            return randomizedValueStrings.AsSpan((RandomValueSize * n) % randomizedValueStrings.Length, RandomValueSize);
        }

        // Write
        public abstract TWriter CreateWriter();
        public abstract bool DisposeWriter(TWriter writer);
        
        public int BenchWrite(SJWriter writer, int iters, int elemCount)
        {
            writer.Reset();

            for (int i = 0; i < iters / 2; i++)
            {
                writer.Reset();

                using (writer.Object())
                {
                    for (int j = 0; j < elemCount; j++) writer.WriteKV(GetRandomKey(i), GetRandomValue(i));
                }
            }
            for (int i = 0; i < iters / 2; i++)
            {
                writer.Reset();

                using (writer.Array())
                {
                    for (int j = 0; j < elemCount; j++) writer.WriteString(GetRandomValue(i));
                }
            }

            return writer.count;
        }

        // Length (generated with default settings) = 15857 for normal, 507559 for large
        [Benchmark]
        public int Write()
        {
            var writer = CreateWriter();
            try
            {
                return BenchWrite(writer, NWriteIters, NWriteElem);
            }
            finally
            {
                DisposeWriter(writer);
            }
        }
        [Benchmark]
        public int WriteLarge()
        {
            var writer = CreateWriter();
            try
            {
                return BenchWrite(writer, NWriteLargeIters, NWriteLargeElem);
            }
            finally
            {
                DisposeWriter(writer);
            }
        }
    }
}
