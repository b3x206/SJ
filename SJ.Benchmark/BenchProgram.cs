using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using System.Text;

namespace SJ.Benchmark
{
    [MemoryDiagnoser]
    public class SJStringReaderBenchmark : SJReaderBenchmark<SJStringReader>
    {
        public string smallText = string.Empty;
        public string largeText = string.Empty;

        public Dictionary<string, SJStringReader> readerDict = [];
        public SJStringReaderBenchmark() : base()
        {
            readerDict[JsonFileValidName] = CreateFromFile(JsonFileValidName);
            readerDict[JsonFileLargeName] = CreateFromFile(JsonFileLargeName);
        }

        public override SJStringReader CreateFromStream(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using var sr = new StreamReader(stream);
            var reader = new SJStringReader(sr.ReadToEnd());
            return reader;
        }
        public override SJStringReader CreateFromFile(string fileName)
        {
            if (readerDict.TryGetValue(fileName, out var v))
            {
                v.Reset();
                return v;
            }

            return base.CreateFromFile(fileName);
        }
        public override bool DisposeReader(SJStringReader reader) => false;
    }

    [MemoryDiagnoser]
    public class SJStringWriterBenchmark : SJWriterBenchmark<SJStringWriter>
    {
        public static readonly StringBuilder sharedBuilder = new(524288);

        public override SJStringWriter CreateWriter()
        {
            sharedBuilder!.Clear();
            return new(sharedBuilder);
        }
        public override bool DisposeWriter(SJStringWriter writer) => false;
    }

    sealed class BenchProgram
    {
        static void Main()
        {
            var config = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));
            BenchmarkRunner.Run([typeof(SJStringReaderBenchmark), typeof(SJStringWriterBenchmark)], config);
        }
    }
}
