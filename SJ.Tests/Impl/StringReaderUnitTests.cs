namespace SJ.Tests.Impl;

[TestClass]
public sealed class StringReaderUnitTests : ReaderUnitTests<SJStringReader>
{
    public override SJStringReader CreateWithFile(string path)
    {
        Assert.IsTrue(File.Exists(path), $"File must exist on path '{path}'");
        return new(File.ReadAllText(path));
    }
    public override SJStringReader CreateWithString(string data) => new(data);    
}
