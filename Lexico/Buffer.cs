using System.Text;
using System;
using System.Collections;
using System.Collections.Generic;
namespace Lexico
{
    public interface IBuffer
    {
        int CodeUnitSize { get; }
        int Length { get; }
        int this[int idx] { get; }
        IEnumerator<int> GetCodepointEnumerator();
        IEnumerator<char> GetCharEnumerator();
    }

    public class StringBuffer : IBuffer
    {
        public StringBuffer(string str)
        {
            this.str = str ?? throw new ArgumentNullException(nameof(str));
        }
        private readonly string str;
        public int CodeUnitSize => 2;
        public int Length => str.Length;
        public int this[int idx] => str[idx];

        public IEnumerator<int> GetCodepointEnumerator()
            => new CodepointEnumerator(str);

        public IEnumerator<char> GetCharEnumerator()
            => str.GetEnumerator();

        private class CodepointEnumerator : IEnumerator<int>
        {
            public CodepointEnumerator(string str)
            {
                this.str = str;
            }
            private readonly string str;
            private int idx;
            public int Current => Char.ConvertToUtf32(str, idx);

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                idx += Char.IsSurrogatePair(str, idx) ? 2 : 1;
                return idx <= str.Length;
            }

            public void Reset()
            {
                idx = 0;
            }
        }
    }

    public class UTF8Buffer : IBuffer
    {
        private byte[] data;
        public int CodeUnitSize => 1;
        public int Length => data.Length;
        public int this[int idx] => data[idx];

        public IEnumerator<int> GetCodepointEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<char> GetCharEnumerator()
        {
            throw new NotImplementedException();
        }

        private class CodepointEnumerator : IEnumerator<int>
        {
            private byte[] data;
            private int idx;
            public int Current
            {
                get
                {
                    var b0 = data[idx];
                    if (b0 < 0x80)
                    {
                        return b0;
                    }
                    int result;
                    result = data[idx + 1] & 0x3F;
                    if (b0 < 0xE0)
                    {
                        return ((b0 & 0x1F) << 6) + result;
                    }
                    result <<= 6;
                    result += data[idx + 2] & 0x3F;
                    if (b0 < 0xF0)
                    {
                        return ((b0 & 0x0F) << 12) + result;
                    }
                    result <<= 6;
                    result += data[idx + 3] & 0x3F;
                    return ((b0 & 0x07) << 18) + result;
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose() {}

            public bool MoveNext()
            {
                var b0 = data[idx];
                idx += b0 < 0x80 ? 1 : (b0 < 0xE0 ? 2 : (b0 < 0xF0 ? 3 : 4));
                return idx <= data.Length;
            }

            public void Reset()
            {
                idx = 0;
            }
        }
    }
}