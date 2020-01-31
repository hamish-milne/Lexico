using System.Text.RegularExpressions;
namespace Lexico
{
    public class NumberAttribute : TerminalAttribute
    {

    }

    internal class NumberParser : IParser
    {

        static readonly Regex regex = new Regex(@"([0-9]*\.)?[0-9]+(e[\-\+]?[0-9]+)?");
        public bool Matches(ref Buffer buffer, ref object value)
        {
            var match = regex.Match(buffer.String, buffer.Position);
            if (match.Success) {
                value = float.Parse(match.Value);
                buffer.Position += match.Value.Length;
                return true;
            }
            return false;
        }
    }
}