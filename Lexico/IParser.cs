namespace Lexico
{
    /// <summary>
    /// A stateless object that consumes text and turns it into values
    /// </summary>
    public interface IParser
    {
        /// <summary>
        /// Matches text at the current position and generates an object value if successful
        /// </summary>
        /// <param name="context">The parse context, modified as the text position advances</param>
        /// <param name="value">The parse result; can have an initial value, but usually overwritten</param>
        /// <returns>True if the parsing was successful, otherwise false</returns>
        bool Matches(ref IContext context, ref object? value);
    }
}