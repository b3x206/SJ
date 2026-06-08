using System.Text;

namespace SJ.Tests;

/// <summary>
/// Class used to recursively iterate the <see cref="SJReader"/> onto a <see cref="StringBuilder"/>.
/// </summary>
public static class ReaderTester
{
    // Top level read must be seperated into "while (!reader.Ended)" and pump SJReader.Value roots
    public static void ReadInner(StringBuilder sb, SJReader reader, SJReader.Value root)
    {
        switch (root.type)
        {
            default:
            case SJType.Error:
                {
                    // Should be able to quit the loop. Note that Ended will also be marked on a case of error.
                    Console.WriteLine($"Error Value: {root}, Ended:{reader.Ended}, Error: '{reader.Error}'");
                    reader.ThrowError();
                    return;
                }
            case SJType.End:
                {
                    // End is no longer an error, but it necessarily isn't the actual file ending.
                    // Something distinct to actual "end" is that the depth is set negative though.
                    if (reader.Ended)
                    {
                        Assert.IsTrue(root.objectDepth < 0, "Depth should be zero if ended");
                    }
                    break;
                }

            case SJType.Comment:
                {
                    // Reading comments require skipping (with explicit Read())
                    // as they can be placed anywhere within the document.
                    sb.Append(root.Slice());
                    break;
                }

            case SJType.Array:
                {
                    bool first = true;
                    sb.Append('[');
                    while (reader.IterateValues(root, out var v))
                    {
                        Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} | {v}({(v.Length > 0 ? v.Slice() : [])})");
                        Assert.AreEqual(v.objectDepth, 2);
                        // Comments don't abide to the LAW
                        if (v.type == SJType.Comment)
                        {
                            ReadInner(sb, reader, v);
                            continue;
                        }

                        // Actual array entries
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, v);

                        first = false;
                    }
                    sb.Append(']');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.Object:
                {
                    bool first = true;
                    sb.Append('{');
                    Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} begin object");
                    while (reader.IterateValues(root, out var v))
                    {
                        Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} | {k}({(k.Length > 0 ? k.Slice() : [])}) = {v}({(v.Length > 0 ? v.Slice() : [])})");
                        // Both K and V are the same
                        if (v.type == SJType.Comment)
                        {
                            Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} skip comment");
                            ReadInner(sb, reader, v);
                            continue;
                        }

                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, k);
                        sb.Append(':');
                        ReadInner(sb, reader, v);

                        first = false;
                        Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} next coment");
                    }
                    sb.Append('}');
                    Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} end object");
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} exit object");
                    break;
                }
            case SJType.String:
                {
                    sb.Append('"').Append(root.Slice()).Append('"');
                    break;
                }
            case SJType.Number:
            case SJType.Null:
            case SJType.Bool:
                {
                    sb.Append(root.Slice());
                    break;
                }
        }
    }
    public const int DefaultSbCapacity = 512;

    public static void Read(SJReader reader, StringBuilder sb, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            sb.Clear();
            var root = reader.Read();
            do
            {
                ReadInner(sb, reader, root);
                root = reader.Read();
            }
            while (!reader.Ended);
        }
        finally
        {
            if (disposeAfterReading && reader is IDisposable d) d.Dispose();
        }
    }
    public static StringBuilder Read(SJReader reader, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var sb = new StringBuilder(DefaultSbCapacity);
        Read(reader, sb, disposeAfterReading);
        return sb;
    }

    public static void ReadJSC(SJReader reader, StringBuilder sb, bool ignoreComments = true, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            sb.Clear();
            reader.allowComments = true;
            reader.ignoreCapturedComments = ignoreComments;
            var root = reader.Read();
            do
            {
                Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} | Begin ReadInner, root: {root}");
                ReadInner(sb, reader, root);
                root = reader.Read();
                Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} | End ReadInner, new root: {root}, reader: {reader}");
            }
            while (!reader.Ended);

            Console.WriteLine($"{DateTime.Now:HH.mm.ss.ffff} | ReadJSC finished");
        }
        finally
        {
            if (disposeAfterReading && reader is IDisposable d) d.Dispose();
        }
    }
    public static StringBuilder ReadJSC(SJReader reader, bool ignoreComments = true, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var sb = new StringBuilder(DefaultSbCapacity);
        ReadJSC(reader, sb, ignoreComments, disposeAfterReading);
        return sb;
    }
}
