namespace Lexico
{
    public interface IParser
    {
        bool Matches(ref IContext context, ref object? value);
    }
}