using System.Text;

namespace SJ.Tests;

/// <summary>
/// Class used to recursively iterate the <see cref="SJReader"/> onto a <see cref="StringBuilder"/>.
/// </summary>
public static class ReaderTester
{
    public static void ReadInner(StringBuilder sb, SJReader reader, SJReader.Value root)
    {
        switch (root.type)
        {
            default:
            case SJType.Error:
                {
                    reader.ThrowError();
                    break;
                }

            case SJType.Array:
                {
                    bool first = true;
                    sb.Append('[');
                    while (reader.IterateArray(root, out var array))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, array);

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
                    while (reader.IterateObject(root, out var k, out var v))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, k);
                        sb.Append(':');
                        ReadInner(sb, reader, v);

                        first = false;
                    }
                    sb.Append('}');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
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
            ReadInner(sb, reader, root);
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

    public static void ReadJSC(SJReader reader, StringBuilder sb, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            sb.Clear();
            reader.allowComments = true;
            var root = reader.Read();
            ReadInner(sb, reader, root);
        }
        finally
        {
            if (disposeAfterReading && reader is IDisposable d) d.Dispose();
        }
    }
    public static StringBuilder ReadJSC(SJReader reader, bool disposeAfterReading = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var sb = new StringBuilder(DefaultSbCapacity);
        ReadJSC(reader, sb, disposeAfterReading);
        return sb;
    }
}
