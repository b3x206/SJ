# SJ .net version

This is a port of https://github.com/rxi/sj.h/blob/master/sj.h with some "fixes" to improve correctness, alongside some added "epic oop inheritance design pattern" to allow reading from data implemented through by extending [`SJReader`](./SJ/SJReader.cs).

This branch (`devel`) is where development is done, before getting packed into "two files". Development of the repository is done by using `git worktree` on seperate directories. This branch can have incomplete code pushed into it, so use with caution! Only the `devel` commits that I tag are "complete"<sup>[Notes##3](#notes)</sup>.

## Notes

#### 1
This is still not a **"100% correct" parser**, what it does with the data (outside of the JSON tokens) ultimately depends on what you read from the parsed chunks. It also _may fail_ on correct or _may not fail_ on wrong sequences. Use at your own risk.

#### 2
Because of
* C#-ifying the code
* Improving "correctness"
* And adding comment support.

The code is **more complicated.** But I have taken the decision to make it simpler on `master`. Pushing here won't update `master` branch (yet), but I will do that sometime, where source is generated on push to `devel` for `master`..

#### 3
"Stable" SJ releases are the ones that are tagged with semver. <br>
_(all tests pass, benchmarks are consistent, slightly worse or slightly improved and it doesn't regress too much)_

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
    if (root.type != SJType.Object) throw new ArgumentException("Vector3 node type must be object", nameof(root));
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

const string data = @"{ ""x"": 42.0, ""y"": -42.0, ""z"": 0.0 }";
var reader = new SJStringReader(data);

SJReader.Value root = reader.Read();
Console.WriteLine($"Saved Position : {ReadVector3(reader, root)}");
```


If processing comments are desired, you should read the document like this: <br>
_In fact, you should read most documents like this._
```cs
using SJ;
using System;

static void InspectConfigData(SJReader reader, SJReader.Value root)
{
    if (root.type != SJType.Object) throw new ArgumentException("Config node type must be object", nameof(root));

    string entrySum = "";
    SJReader.Value key = SJReader.Value.Error();
    while (reader.IterateObjectEntries(root, out var type, out var value))
    {
        switch (type)
        {
            case SJReader.EntryType.None:
                const string SummaryTk = "@summary";
                var comment = value.Slice(2, value.Length - 2).Trim();
                if (comment.StartsWith(SummaryTk))
                {
                    entrySum = new string(comment.Slice(SummaryTk.Length).Trim());
                }
                continue;
            case SJReader.EntryType.Key:
                key = value;
                continue;
            case SJReader.EntryType.Value:
                Console.WriteLine($"Summary: {entrySum}\n  {new string(key.Slice())} = {new string(value.Slice())}");
                continue;
        }
    }
}

const string data = @"// The problem with comments is that they can appear anywhere.
// Which makes some things that were implicit, explicit now.
{
    // Comments in JSON doesn't make much sense actually. Just use ini
    /* @summary This field is 42, and actually you can put this within the JSON, but whatever.. */
    ""is42"": 42
}
// And to validate EOF, do this (it was implicit with the last Read()) ↓
";
var reader = new SJStringReader(data)
{
    ignoreCapturedComments = false // opt-in to the new behaviour
};

SJReader.Value root = reader.Read();
do
{
    if (root.type == SJType.Comment) continue; // Skip root level comments.
    InspectConfigData(reader, root);
    root = reader.Read(); // to not get stuck in an infinite loop
}
while (!reader.Ended);
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

Actually not success, entire code went kaput :(

Use the [`SJStringWriter`](SJ/SJStringWriter.cs) class to get started.

```cs
using SJ;
using System;
using System.Numerics;
using System.Globalization;

static Vector3 ReadVector3(SJReader reader, SJReader.Value root) ... // Copy the method from above Vector3 reader

// Serialize an arbitrary Vector3
var writer = new SJStringWriter()
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
string data = writer.ReadData();
var reader = new SJStringReader(data);
position = ReadVector3(reader, reader.Read());

// Use ReadData to read the resulting string.
Console.WriteLine($"Saved Position : {data}, Read Position : {position}");
```

### Benchmarks
**TODO : Outdated**

* Read = Read a [small file (valid.json)](TestFiles/valid.json) 256 times
* ReadLarge = Read a [large file (5mb.json)](TestFiles/5mb.json) 16 times
* Write = Write few kb of JSON (`{ "random_key": "random_value", ... 512 entries }`) 256 times
* WriteLarge = Write ~500kb JSON (`{ "random_key": "random_value", ... 16384 entries }`) 16 times

Classes used are [`SJStringReader`](./SJ/SJStringReader.cs) and [`SJStringWriter`](SJ/SJStringWriter.cs)

```
BenchmarkDotNet v0.15.8, Linux Debian GNU/Linux 13 (trixie)
13th Gen Intel Core i7-13620H 2.92GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 8.0.421
  [Host]     : .NET 8.0.27 (8.0.27, 8.0.2726.22922), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.27 (8.0.27, 8.0.2726.22922), X64 RyuJIT x86-64-v3
```
| Method     | Mean         | Error      | StdDev     | Allocated |
|----------- |-------------:|-----------:|-----------:|----------:|
| Read       |  29841.4 μs |   475.8 μs |   445.0 μs |     136 B |
| ReadLarge  | 181649.1 μs |  3476.7 μs |  4641.4 μs |     136 B |
| Write      |  18381.9 μs |   115.7 μs |   102.5 μs |     152 B |
| WriteLarge |  32507.3 μs |   245.2 μs |   217.4 μs |     152 B |

Reader (according to ReadLarge) provides a ~464 MB/s throughput.
Writer (according to WriteLarge) provides a ~182 MB/s throughput.

The allocations are caused by creating a [`Stack<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1?view=netstandard-2.1&devlangs=csharp) when the base SJ class is initialized. <br>
**Note:** More allocations may occur depending on the size or strings you allocate while creating an object or while writing into a resizing buffer in memory. Also the `static readonly` initializations for some lambdas within classes, but those are done only once on the program's lifetime..

Note that these are tested in the best case scenario, real life applications and usage will cause differences in speed. <br>
_Lack of performance_ could be caused by excessive bound checking for the Reader and the source the Writer writes into..

---

More examples are in the [`./SJ/Examples`](./SJ/Examples) directory. <br>
These files are not included for compiling, you can copy and paste it normally.

You can also use the [unit tests](./SJ.Tests) as examples too.

---

## Changelog
### - 1.0.0
* Initial release as "SJ".
* Somewhat janky unit tests, implicit/automated validation of files that were partially broken (is too permissive).
* But still functional and can technically parse large valid JSON documents and JSC (Comments not captured).

### - 2.0.0
**SJReader**
* More explicit API (Opt-in via `SJReader.ignoreCapturedComments = false`)
* TODO : Lots of things. Off my mind, changes are more explicit usage of API, more correctness checks (that I thought were being handled), improved unit tests, old code has to be modified (to handle new SJ cases) and comments can be now captured.
* Do a migration guide as well. (for the 1 user of the library, which is me)
* Rename to "BXSJ"

<details>

<summary> Migration from 1.x -> 2.x </summary>

1. **Object keys now return an explicit value of `SJType.Key`!** <br>
   This is easy to migrate from, if your `switch` or `if` cases check for `SJType.String` as key, it can be simply interchanged to it such as:
```cs
static void Process(SJReader reader, SJReader.Value v)
{
    switch (v.type)
    {
        default:
            throw new ArgumentException($"Invalid type {v.type}", nameof(v));

        // ... cases for other SJTypes available in 1.x
        // ❌ : Will fail on an event of SJType.Key!
        case SJType.String:
            Console.WriteLine(new string(v.Slice()));
            break;
    }
}

// ...
while (reader.IterateObject(obj, out var k, out var v))
{
    // ↓ Will throw! However, if you do not care about the type of the key, 
    //   this isn't a problem as it will behave exactly like SJType.String.
    Process(reader, k);
    Process(reader, v);
}

// ↓ Instead do this
static void Process(SJReader reader, SJReader.Value v)
{
    switch (v.type)
    {
        default:
            throw new ArgumentException($"Invalid type {v.type}", nameof(v));

        // ... cases for other SJTypes available in 1.x
        // ✅ : Will not fail on an event of SJType.Key
        case SJType.Key:
        case SJType.String:
            Console.WriteLine(new string(v.Slice()));
            break;
    }
}
```
2. **Only applicable if you have used `SJReader.allowComments = true`, even with that there are cases where the old implicit behaviour is used.** <br>
   Because any `SJReader` may have it's `ignoreCapturedComments` set as `false`, (which may require explicit calls to `Read` while `!SJReader.ended`), 
   in a case where a "comment" is encountered on the root level, it will be not automatically skipped and will be pushed into your main reader function as a value.
   This may cause problems in such cases like:
```cs
static void Process(SJReader reader, SJReader.Value v)
{
    switch (v.type)
    {
        default:
            throw new ArgumentException($"Invalid type {v.type}", nameof(v));

        // ... cases for other SJTypes available in 1.x
        // ❌ : Will fail on a case of SJType.Comment (and Key)!
        case SJType.Number:
            Console.WriteLine($"Number: {new string(v.Slice())}");
            break;
        case SJType.Array:
            while (reader.IterateArray(v, out var av)) 
            {
                Process(reader, v);
            }
            break;
    }
}

const string Data = @"// I decided to allow comments for some reason
[1.0, 2.0]
// Oh well..
";
var reader = new SJStringReader(Data)
{
    allowComments = true,
    captureComments = true // Not knowing we are interacting with older code
};

// And this deserialization code is not aware of "captureComments"
// ❌ : The Read will have SJType.Comment supplied into it!
var root = reader.Read();
Process(reader, root);

// ↓ Instead do this
for (var root = reader.Read(); !reader.ended; root = reader.Read())
{
    // ✅ : Discard comments when interacting with code that disallow or not expect it
    if (root.type == SJType.Comment) continue;
    ReadPos(reader, root);
}
```

3. Writing comments with `SJWriter` is easier now, you no longer need to use hacky `SJWriter.WriteLiteralValue`:
```cs
// 1. Enable it
writer.allowComments = true;
// 2. Write it (if you don't enable it before it will throw)
writer.WriteComment("This is a comment!");
writer.WriteCommentLine("This is a comment that starts with //!");

// Note : Writer tracks whether if a comment is written (through WrittenComment). If commented json is undesired you 
// can "false assert" that. **However it won't capture any indirect way of writing comments, so be careful!**
```

From 1.x, everything else is more or less the same, except the inner works of . <br>
**Note:** However in the future, implicit behaviour _could be_ removed. It is recommended to use the new way of iterating objects.

</details>
