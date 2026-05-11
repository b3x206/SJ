# SJ .net version

This is a port of https://github.com/rxi/sj.h/blob/master/sj.h with some "fixes" to improve correctness, alongside some added "epic oop inheritance design pattern" to allow reading from data implemented through by extending [`SJReader`](./SJ/SJReader.cs).

## Notes

**1:**

This is still not a "100% correct" parser, what it does with the data (outside of the JSON tokens) ultimately depends on what you read from the parsed chunks.

**2:**

Because of
* C#-ifying the code
* Improving "correctness"
* And adding comment support.

The parser implementation is not very simple like sj.h. If you want to have a simpler JSON reader, 
you can check the `no-jsonc` branch (WIP) for the version without comment support (even simpler version).

**3:**

If you need / want a `SJWriter`, you can check the `full` branch (WIP), instead of `master`, which only has the reader (because well, "simple").

## Usage
Use the [`SJStringReader`](./SJ/SJStringReader.cs) class to get started.

## Examples
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

More complex examples are in the [`SJ/Examples`](./SJ/Examples) directory. These files are not included for compiling, you can copy and paste it normally.

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
