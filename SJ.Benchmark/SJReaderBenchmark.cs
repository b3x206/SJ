using BenchmarkDotNet.Attributes;
using System.Diagnostics;

namespace SJ.Benchmark
{
    public abstract class SJReaderBenchmark<TReader> where TReader : SJReader
    {
        public const int NReadIters = 256;
        public const int NReadLargeIters = 32;

        public const string JsonFileValidName = "valid.json";
        public const string JsonFileLargeName = "5mb.json";

        public static Stream LoadFromAssembly(string fileName)
        {
            Stream? s = typeof(BenchProgram).Assembly.GetManifestResourceStream(fileName);
            Debug.Assert(s != null, "Assembly stream with fileName must exist.");
            return s;
        }

        public abstract TReader CreateFromStream(Stream stream);
        public virtual TReader CreateFromFile(string fileName)
        {
            // If your data is "cached" on something like [GlobalSetup] and the
            // JSON system works on strings, you can just return according to a Dictionary, with resetting on return as well (object pooling).
            // We don't want to measure data loading overhead, as yes it's bad, but if that is "relevant" to your reader, you can benchmark that load portion too
            return CreateFromStream(LoadFromAssembly(fileName));
        }
        public abstract bool DisposeReader(TReader reader);

        // Read
        // * The reader is reused to provide the "best case scenario"
        // * Reset MUST NOT delete loaded data on Reader. (Reader's state IS position, Writer's state IS data, so that one "deletes" the data written.
        // If undesired, flush [if applicable] or read and retain the data) Pass the tests with your type before benchmarking.
        public static void ReadRecursiveInternal(SJReader reader, SJReader.Value value, ref long counter)
        {
            switch (value.type)
            {
                case SJType.Comment:
                case SJType.Number:
                case SJType.Key:
                case SJType.String:
                case SJType.Bool:
                case SJType.Null:
                    unchecked { counter += value.Slice().Length; }
                    break;

                case SJType.Object:
                    SJReader.Value k = SJReader.Value.Error();
                    while (reader.IterateObjectEntries(value, out var v))
                    {
                        switch (v.type)
                        {
                            case SJType.Key:
                                k = v;
                                continue;
                            case SJType.Comment:
                                ReadRecursiveInternal(reader, v, ref counter);
                                continue;
                            // This method is guaranteed to not return error, if the while loop evaluates correctly.
                            default:
                                ReadRecursiveInternal(reader, v, ref counter);
                                continue;
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
        public static void ReadRecursive(SJReader reader, ref long counter) => ReadRecursiveInternal(reader, reader.Read(), ref counter);
        public static long BenchReadN(SJReader reader, int count)
        {
            long counter = 0;
            for (int i = 0; i < count; i++)
            {
                ReadRecursive(reader, ref counter);
                reader.Reset();
            }

            return counter;
        }

        [Benchmark]
        public long Read()
        {
            var reader = CreateFromFile(JsonFileValidName);
            try
            {
                return BenchReadN(reader, NReadIters);
            }
            finally
            {
                DisposeReader(reader);
            }
        }
        [Benchmark]
        public long ReadLarge()
        {
            var reader = CreateFromFile(JsonFileLargeName);
            try
            {
                return BenchReadN(reader, NReadLargeIters);
            }
            finally
            {
                DisposeReader(reader);
            }
        }
    }
}
