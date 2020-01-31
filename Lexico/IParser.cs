namespace Lexico
{
    internal interface IParser
    {
        bool Matches(ref Buffer buffer, ref object value);
    }
}