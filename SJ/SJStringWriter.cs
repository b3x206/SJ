using System;
using System.Text;

namespace SJ
{
    /// <summary>
    /// Writes JSON to a <see cref="StringBuilder"/>
    /// </summary>
    public sealed class SJStringWriter : SJWriter
    {
        private readonly StringBuilder sb;
        public const int DefaultCapacity = 256;

        public SJStringWriter(StringBuilder sb)
        {
            this.sb = sb ?? throw new System.ArgumentNullException(nameof(sb));
        }
        public SJStringWriter()
        {
            sb = new StringBuilder(DefaultCapacity);
        }
        public SJStringWriter(int capacity)
        {
            sb = new StringBuilder(capacity);
        }
        public SJStringWriter(int capacity, int maxCapacity)
        {
            sb = new StringBuilder(capacity, maxCapacity);
        }

        public override void Append(char c) => sb.Append(c);
        public override void Append(ReadOnlySpan<char> s) => sb.Append(s);

        public override void Reset()
        {
            base.Reset();
            sb.Clear();
        }
        public override string ToString()
        {
            return sb.ToString();
        }
    }
}
