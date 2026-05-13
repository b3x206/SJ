# SJ .net version

This is a port of https://github.com/rxi/sj.h/blob/master/sj.h with some "fixes" to improve correctness, alongside some added "epic oop inheritance design pattern" to allow reading from data implemented through by extending [`SJReader`](./SJ/SJReader.cs).

This branch (`full`) also adds a [`SJWriter`](./SJ/SJWriter.cs) to allow writing JSON with rule enforcement, alongside auto formatting if needed.

## Notes

**1:**

This is still not a **"100% correct" parser**, what it does with the data (outside of the JSON tokens) ultimately depends on what you read from the parsed chunks.

**2:**

Because of
* C#-ifying the code
* Improving "correctness"
* And adding comment support.

The parser implementation is not very simple like sj.h. If you want to have a simpler JSON reader, 
you can check the `no-jsonc` branch (WIP) for the version without comment support (even simpler version).

## Examples

### Reader
Use the [`SJStringReader`](./SJ/SJStringReader.cs) class to get started.

```cs
using SJ;
using System;
// You can read "root level" values. That is : bool, null, string and number.
// Numbers are always seperated with '.'
var datas = new string[] { "true", "false", "null", @"""Hello world!""", "123.456" };
for (int i = 0; i < datas.Length; i++)
{
    var data = datas[i];
    var reader1 = new SJStringReader(data);

    SJReader.Value value = reader1.Read();
    Console.WriteLine($"type : {value.type}, data : {new string(value.Slice())}");
}

// Arrays and strings can be iterated as shown
var arrayData = "[1, 2, 3, 4, 5, 6]";
var reader = new SJStringReader(arrayData);
SJReader.Value root = reader.Read();
while (reader.IterateArray(root, out SJReader.Value value))
{
    Console.WriteLine($"type : {value.type}, data : {new string(value.Slice())}");
}

var objectData = @"{ ""a"": 1, ""b"": 2, ""c"": 3 }";
reader.Data = objectData; // The reader will reset automatically
while (reader.IterateObject(root, out SJReader.Value key, out SJReader.Value value))
{
    Console.WriteLine($"key   | type : {key.type}, data : {new string(key.Slice())}");
    Console.WriteLine($"value | type : {value.type}, data : {new string(value.Slice())}");
}

// To recurse these, you can pass the resulting value into a "reader function"
// For more detailed usage, check SJ/Examples/Printer.cs and SJ/Examples/Tree.cs
```

```cs
// A basic object deserialization example. Non-recursive.
using SJ;
using System;
using System.Numerics;
using System.Globalization;

var data = @"{ ""x"": 42.0, ""y"": -42.0, ""z"": 0.0 }";
var reader = new SJStringReader(data);

// If the data schema is "known", you can just read like this:
var result = new Vector3();
SJReader.Value root = reader.Read();

while (reader.IterateObject(root, out SJReader.Value key, out SJReader.Value value))
{
    switch (char.ToLower(key.Slice()[0]))
    {
        // CultureInfo stuff is if your country seperates decimals with ',' instead of '.'
        case 'x': result.X = float.Parse(value.Slice(), CultureInfo.InvariantCulture); break;
        case 'y': result.Y = float.Parse(value.Slice(), CultureInfo.InvariantCulture); break;
        case 'z': result.Z = float.Parse(value.Slice(), CultureInfo.InvariantCulture); break;
        default: throw new Exception($"Invalid key : {key}");
    }
}

Console.WriteLine(result);
```

---

Unlike the original SJ, there is also a string escaping implementation if desired:

```cs

// ...
switch (SJReader.Value.type)
{
    // ...
    case SJType.String:
    {
        string result = SJEscape.Unescape(SJReader.Value.Slice());
        // `SJEscape.Escape(string | ReadOnlySpan<char>)` is for the other way around...
        break;
    }
}
```

This is used as the default string escape for the Writer.

### Writer
Use the [`SJStringBuilderWriter`](./SJ/SJStringBuilderWriter.cs) class to get started.

```cs
using SJ;

var writer = new SJStringBuilderWriter()
{
    // Enable pretty printing
    indentSize = 4,
    // Don't throw on error for now
    ThrowOnError = false,
};
// Writing any of the following in the root is possible, but:
writer.Write(123.123);
// After this value, the depth is zero. Meaning it's no longer to write anything later.
// (because ThrowOnError is false, these will silently fail, setting writer.Error only)
writer.Write(123123123L);
writer.Write(123123123UL);
writer.Write("Hello!");
using (writer.Array()) { }
using (writer.Object()) { }

// .. erroreneous writes will require a reset, when not throwing.
// Each "Write" method returns a boolean, it will return false.
writer.Reset();
// Let's throw exceptions for the rest of the example.
writer.ThrowOnError = true;

// Arrays are written like this:
using (writer.Array()) // Or writer.BeginArray();
{
    writer.Write(1);
    writer.Write(2);
    writer.Write(3);
    writer.Write(4);
    writer.Write(5);
} // writer.EndArray();
  // ↑ The array will end automatically once you go out of using scope.

using (writer.Object()) // Or writer.BeginObject();
{
    // You must write key the following way:
    // 1 (recommended) :
    writer.WriteKey("key");
    // 2 (does the same thing as WriteKey if a key is needed):
    // writer.WriteString("key");
    // 3 (not recommended, if your key is null object you will get an exception instead):
    // writer.Write("key");
    // Note that there isn't a built in check for duplicate keys.
    // > You should be careful with it, or modify/override WriteKey to track that.
    // ---
    // Then write your value. You can recurse to create tree structures, but I will just write a basic "value" instead
    writer.Write("my nice value");
    // If you end your object without writing a "value" after a "key" was written, you will cause an error so beware.

    // It is possible to nest objects. It will also be auto indented too with pretty printing:
    writer.WriteKey("numbers that i like");
    using (writer.Object())
    {
        writer.WriteKey("in childhood");
        using (writer.Array())
        {
            writer.Write(3.141592); // Larp pro max
            writer.Write(2); // It's nice
            writer.Write(5); // Was my first favourite
            writer.Write(8); // Also good
        }

        // Now I fail math. But dw I don't hate it, because I'm not a mathmetician
        writer.WriteKey("currently");
        writer.WriteNull();
    }
} // writer.EndObject();
  // ↑ The object will end automatically once you go out of using scope.

// Check ./SJ/Examples/Tree.cs for a tree with JSON serialization and deserialization.
```

---

More examples are in the [`./SJ/Examples`](./SJ/Examples) directory. <br>
These files are not included for compiling, you can copy and paste it normally.

You can also use the unit tests

## TODO
* [ ] ?? : Create noexcept tests (but not that important - error has to be set to throw 
  exception in normal cases. so maybe i will do it only for the Writer.)
* [ ] Backport improvements to the tests on `full` back to `master` branch
* [ ] Create `no-jsonc` based on that
* [ ] Publish as nuget (?)
