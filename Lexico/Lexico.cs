using System;

namespace Lexico
{
    /// <summary>
    /// Simplified parsing API
    /// </summary>
    public static class Lexico
    {
        /// <summary>
        /// Parses a value of the given Type (T) from an input string.
        /// Throws a FormatException if the parsing fails.
        /// </summary>
        /// <param name="str">The input text</param>
        /// <param name="trace">Where to log the parser trace</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The parsed value</returns>
        public static T Parse<T>(string str, ITrace trace)
        {
            if (TryParse<T>(str, out var output, trace)) {
                return output;
            } else {
                throw new FormatException($"Value could not be parsed as {typeof(T)}");
            }
        }

        /// <summary>
        /// Attempts to parse a value of the given Type (T) from an input string
        /// </summary>
        /// <param name="str">The input text</param>
        /// <param name="output">The output value, if successful</param>
        /// <param name="trace">Where to log the parser trace</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the parsing succeeded, otherwise false</returns>
        public static bool TryParse<T>(string str, out T output, ITrace trace)
        {
            var success = TryParse(str, typeof(T), out var temp, trace);
            output = success ? (T) temp : default!;
            return success;
        }

        public static bool TryParse(string str, Type outputType, out object output, ITrace trace)
        {
            object? value = null;
            IContext context = Context.CreateRoot(str, trace);
            var result = ParserCache.GetParser(outputType).MatchChild(null, ref context, ref value);
            output = result ? value! : default!;
            return result;
        }
    }
}
