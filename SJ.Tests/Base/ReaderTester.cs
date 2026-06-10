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
                        Assert.IsTrue(root.objectDepth <= 0, "Depth must be zero if ended");
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
                    while (reader.IterateArrayEntries(root, out var v))
                    {
                        // Skipping comments on array is easier. Technically could also do
                        // the "ObjectEntry type" sieve for array as well.
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

                    SJReader.Value k = SJReader.Value.Error();
                    while (reader.IterateObjectEntries(root, out var v))
                    {
                        switch (v.type)
                        {
                            case SJType.Comment:
                                ReadInner(sb, reader, v);
                                continue;
                            case SJType.Key:
                                k = v;
                                continue;
                            default:
                                if (!first)
                                    sb.Append(',');

                                // k must be valid.
                                ReadInner(sb, reader, k);
                                sb.Append(':');
                                ReadInner(sb, reader, v);

                                first = false;
                                continue;
                        }
                    }
                    sb.Append('}');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.Key:
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
            while (!reader.Ended)
            {
                ReadInner(sb, reader, root);
                root = reader.Read();
            }
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
            while (!reader.Ended)
            {
                ReadInner(sb, reader, root);
                root = reader.Read();
            }
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
