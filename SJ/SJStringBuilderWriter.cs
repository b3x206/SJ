using System.Text;

namespace SJ
{
    /// <summary>
    /// Writes JSON to a <see cref="StringBuilder"/>
    /// </summary>
    public sealed class SJStringBuilderWriter : SJWriter
    {
        private readonly StringBuilder sb;
        public const int DefaultCapacity = 512;

        public SJStringBuilderWriter(StringBuilder sb)
        {
            this.sb = sb ?? throw new System.ArgumentNullException(nameof(sb));
        }
        public SJStringBuilderWriter()
        {
            sb = new StringBuilder(DefaultCapacity);
        }
        public SJStringBuilderWriter(int capacity)
        {
            sb = new StringBuilder(capacity);
        }
        public SJStringBuilderWriter(int capacity, int maxCapacity)
        {
            sb = new StringBuilder(capacity, maxCapacity);
        }

        public override void Append(char c) => sb.Append(c);
        public override void Append(string s) => sb.Append(s);

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
