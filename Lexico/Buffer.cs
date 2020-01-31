namespace Lexico
{
    internal struct Buffer
    {
        public Buffer(string str) {
            String = str;
            Position = 0;
        }
        public string String { get; }
        public int Position { get; set; }
        public char? Peek(int index) =>
            (Position + index < String.Length) ? String[Position + index] : default;
    }
}