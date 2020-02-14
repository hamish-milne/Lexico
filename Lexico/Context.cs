using System;
using System.Collections.Generic;

namespace Lexico
{
    /// <summary>
    /// The parse context; orchestrates the parser behaviour. Typically handles caching and recursion.
    /// </summary>
    public interface IContext
    {
        /// <summary>
        /// The input text in its entirety
        /// </summary>
        /// <value></value>
        string Text { get; }

        /// <summary>
        /// The current position within the input string
        /// </summary>
        /// <value></value>
        int Position { get; }

        /// <summary>
        /// Looks ahead of the current position by the given amount
        /// </summary>
        /// <param name="offset"></param>
        /// <returns>The character at the offset, or Null if past the EOF</returns>
        char? Peek(int offset);

        /// <summary>
        /// Advances the text position by the given amount, returning a new Context
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        IContext Advance(int length);

        /// <summary>
        /// Performs a match with the given parser. This method allows caching and recursion to be managed
        /// as needed.
        /// </summary>
        /// <param name="parser">The parser in question</param>
        /// <param name="name">A human-readable identifier for the child relationship, for the log</param>
        /// <param name="newContext">The context after parsing. May be different even on failure</param>
        /// <param name="value">The parser output value</param>
        /// <returns>True if the text matches, otherwise false</returns>
        bool MatchChild(IParser? parser, string? name, out IContext newContext, ref object? value);
    }

    public static class ContextExtensions
    {
        /// <summary>
        /// Performs a match with the given parser. This method allows caching and recursion to be managed
        /// as needed. (This is a convenience extension method for IContext.MatchChild)
        /// </summary>
        /// <param name="parser">The parser in question</param>
        /// <param name="name">A human-readable identifier for the child relationship, for the log</param>
        /// <param name="newContext">The context after parsing. May be different even on failure</param>
        /// <param name="value">The parser output value</param>
        /// <returns>True if the text matches, otherwise false</returns>
        public static bool MatchChild(this IParser? parser, string? name, ref IContext context, ref object? value)
            => context.MatchChild(parser, name, out context, ref value);
    }

    internal sealed class Context : IContext
    {
        private Context() {
            trace = null!;
            cache = null!;
            Text = null!;
        }

        private Context? parent;
        private IParser? parser;
        private ITrace trace;
        private Dictionary<IParser, (bool, object?, int)> cache;

        public string Text { get; private set; }
        public int Position { get; private set; }

        public IContext Advance(int length)
        {
            if (length < 0) {
                throw new ArgumentOutOfRangeException();
            }
            var child = Get();
            child.Position += length;
            // TODO: Avoid allocs here
            child.cache = new Dictionary<IParser, (bool, object?, int)>();
            return child;
        }

        public char? Peek(int offset)
            => (Position + offset) < Text.Length ? Text[Position + offset] : default(char?);

        private static readonly Stack<Context> pool
            = new Stack<Context>();

        private static Context Acquire()
        {
            Context? obj = null;
            lock (pool) {
                if (pool.Count > 0) {
                    obj = pool.Pop();
                }
            }
            if (obj == null) {
                obj = new Context();
            }
            return obj;
        }

        private Context Get()
        {
            var obj = Acquire();
            obj.Text = Text;
            obj.Position = Position;
            obj.parent = this;
            obj.trace = trace;
            return obj;
        }

        private void Release()
        {
            parent = null;
            parser = null!;
            trace = null!;
            cache = null!;
            lock (pool) {
                pool.Push(this);
            }
        }

        public static Context CreateRoot(string text, ITrace trace)
        {
            var obj = Acquire();
            obj.Text = text;
            obj.trace = trace;
            obj.cache = new Dictionary<IParser, (bool, object?, int)>();
            return obj;
        }

        public bool MatchChild(IParser? parser, string? name, out IContext newContext, ref object? value)
        {
            if (cache == null) {
                throw new ObjectDisposedException("The Context was released");
            }
            // TODO: Don't do this:
            if (parser is UnaryParser u) {
                parser = u.Inner;
            }
            newContext = this;
            if (parser == null) {
                return true;
            }
            var prevValue = value;
            // Block any immediate left recursion, and
            // block any parsing already confirmed invalid
            // TODO: Also block non-immediate left recursion X levels deep
            Context? c = this;
            while (c != null && Position == c.Position) {
                if (c.parser == parser) {
                    return false;
                }
                if (c.cache.TryGetValue(parser, out var cacheValue)) {
                    if (cacheValue.Item1) {
                        value = cacheValue.Item2;
                        trace.Push(parser, name);
                        trace.Pop(parser, true, cacheValue.Item2, new StringSegment(Text, Position, cacheValue.Item3));
                        newContext = Advance(cacheValue.Item3);
                    }
                    return cacheValue.Item1;
                }
                c = c.parent;
            }

            // Acquire a child context
            // TODO: A better solution for choosing whether to push a parser onto the stack
            var concrete = !(parser is AlternativeParser) && !(parser is OptionalParser);
            if (concrete) {
                var child = Get();
                child.parser = parser;
                child.cache = cache;
                newContext = child;
            }

            // Trace and match the child parser
            trace.Push(parser, name);
            var result = parser.Matches(ref newContext, ref value);
            if (newContext == null) {
                throw new InvalidOperationException($"{parser} set the context to null");
            }
            var chars = Position >= Text.Length
                ? default
                : new StringSegment(Text, Position, Math.Max(1, newContext.Position - Position));
            trace.Pop(parser, result, value, chars);

            // Remember the result
            if (concrete)
            {
                if (result) {
                    cache[parser] = (true, value, newContext.Position - Position);
                } else {
                    cache.Add(parser, (false, null, 0));
                }
            }
            // Restore the last Value on failure
            value = result ? value : prevValue;

            // We release and 'pop' the created context if:
            //   - the parse failed (we revert to the last state)
            //   - nothing was consumed (to not unnecessarily grow the stack)
            if (newContext != this && (!result || newContext.Position == Position)) {
                ((Context)newContext).Release();
                newContext = this;
            }
            return result;
        }
    }
}
