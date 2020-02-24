using System.Reflection;
using System.Collections.Concurrent;
using System;

namespace Lexico
{
    /// <summary>
    /// Simplified parsing API
    /// </summary>
    public class Lexico
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
            if (!compilerCache.TryGetValue(typeof(T), out var compiled)) {
                var parser = ParserCache.GetParser(typeof(T));
                compiled = CompileContext.Compile(parser, true); // TODO: Optimization options etc.
                Console.WriteLine($"Approx complexity: {GetILBytes(compiled.Method).Length}");
                compilerCache.TryAdd(typeof(T), compiled);
            }
            output = default!;
            int position = 0;
            return ((Parser<T>)compiled)(str, ref position, ref output, trace);
        }

        private static byte[] GetILBytes(MethodInfo method)
        {
            var dynamicMethod = method.GetType().GetField("m_owner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(method);
            var resolver = dynamicMethod.GetType().GetField("m_resolver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dynamicMethod);
            if (resolver == null) throw new ArgumentException("The dynamic method's IL has not been finalized.");
            return (byte[])resolver.GetType().GetField("m_code", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resolver);
        }

        private static readonly ConcurrentDictionary<Type, Delegate> compilerCache
            = new ConcurrentDictionary<Type, Delegate>();
    }
}
