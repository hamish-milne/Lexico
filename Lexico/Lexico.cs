﻿using System.Collections.Concurrent;
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
            var compiled = Compile(typeof(T), trace != null);
            int position = 0;
            output = default!;
            return ((Parser<T>)compiled)(str, ref position, ref output, trace!, userObject);
        }

        private static Delegate Compile(Type type, bool hasTrace)
        {
            var key = (type, hasTrace);
            if (!compilerCache.TryGetValue(key, out var compiled)) {
                var parser = ParserCache.GetParser(type);
                compiled = CompileContext.Compile(parser, hasTrace ? CompileFlags.Trace : CompileFlags.None);
                compilerCache.TryAdd(key, compiled);
            }
            return compiled;
        }

        public static bool TryParse(string str, Type outputType, out object output, ITrace? trace = null, object? userObject = null)
        {
            var compiled = Compile(outputType, trace != null);
            var args = new object[]{str, 0, null!, trace!, userObject!};
            var result = (bool)compiled.DynamicInvoke(args);
            output = args[2];
            return result;
        }

        private static readonly ConcurrentDictionary<(Type, bool), Delegate> compilerCache
            = new ConcurrentDictionary<(Type, bool), Delegate>();
    }
}
