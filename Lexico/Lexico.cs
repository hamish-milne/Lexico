using System;
using System.Collections.Generic;

namespace Lexico
{
    public interface Entry
    {
        bool TryParse(IParser parser, IEnumerable<Feature> features, string input, out object? output, ITrace? trace, object? userObject);
    }

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
        /// <param name="userObject">User object which will populate any <see cref="UserObjectAttribute"/> terms</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The parsed value</returns>
        public static T Parse<T>(string str, ITrace? trace = null, object? userObject = null)
        {
            if (TryParse<T>(str, out var output, trace, userObject)) {
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
        /// <param name="userObject">User object which will populate any <see cref="UserObjectAttribute"/> terms</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the parsing succeeded, otherwise false</returns>
        public static bool TryParse<T>(string str, out T output, ITrace? trace = null, object? userObject = null)
        {
            var result = TryParse(str, typeof(T), out var objOut, trace!, userObject);
            if (result) {
                output = (T)objOut!;
            } else {
                output = default(T)!;
            }
            return result;
        }

        private static readonly Entry entry = new Runtime();

        public static bool TryParse(string str, Type outputType, out object? output, ITrace? trace = null, object? userObject = null)
        {
            var features = new List<Feature> {
                new String(),
                new UserObject(),
                new Recursive(),
                new StartPosition(),
                new CheckILR()
            };
            if (trace != null) {
                features.Add(new Trace());
            }
            return entry.TryParse(ParserCache.GetParser(outputType), features, str, out output, trace, userObject);
        }
    }
}
