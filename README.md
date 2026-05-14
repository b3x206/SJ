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
you can check the `no-jsonc` branch (WIP) for the version without comment support
<sup>(even simpler version, but it will still have the correctness improvements)</sup>.

## Examples

### Reader
Use the [`SJStringReader`](./SJ/SJStringReader.cs) class to get started.

```cs
// A basic object deserialization example. Non-recursive.
using SJ;
using System;
using System.Numerics;
using System.Globalization;

static Vector3 ReadVector3(SJReader reader, SJReader.Value root)
{
    Vector3 result = Vector3.Zero;

    // If the data schema is "known", you can just read like this:
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
    return result;
}

var data = @"{ ""x"": 42.0, ""y"": -42.0, ""z"": 0.0 }";
var reader = new SJStringReader(data);

SJReader.Value root = reader.Read();
Console.WriteLine($"Saved Position : {ReadVector3(reader, root)}");
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
using System;
using System.Numerics;
using System.Globalization;

static Vector3 ReadVector3(SJReader reader, SJReader.Value root) ... // Copy the method from above

// Serialize an arbitrary Vector3
var writer = new SJStringBuilderWriter()
{
    // Enable pretty printing
    indentSize = 4,
};
var position = new Vector3(1f, 2f, 3f);
using (writer.Object())
{
    writer.WriteKV("X", position.X);
    writer.WriteKV("Y", position.Y);
    writer.WriteKV("Z", position.Z);
}

// Now deserialize it
position = Vector3.Zero;
var reader = new SJStringReader(writer.ToString());
position = ReadVector3(reader, reader.Read());

Console.WriteLine($"Saved Position : {writer}, Read Position : {position}");
```

---

More examples are in the [`./SJ/Examples`](./SJ/Examples) directory. <br>
These files are not included for compiling, you can copy and paste it normally.

You can also use the [unit tests](./SJ.Tests) as examples too.

## TODO
* [ ] ?? : Create noexcept tests (but not that important - error has to be set to throw 
  exception in normal cases. so maybe i will do it only for the Writer.)
* [ ] Backport improvements to the tests on `full` back to `master` branch
* [ ] Create `no-jsonc` based on that
* [ ] Publish as nuget (?)
* [ ] Make readme.md less threatening to look at (partially done)
