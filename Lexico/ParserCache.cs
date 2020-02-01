using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace Lexico
{
    internal class UnaryParser : IParser, IUnaryParser
    {
        public IParser Inner { get; private set; }

        public void Set(IParser inner) {
            if (Inner != null) {
                throw new InvalidOperationException("Already set");
            }
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            if (Inner == null) {
                throw new InvalidOperationException("Not filled yet");
            }
            return Inner.Matches(ref buffer, ref value, trace);
        }

        public override string ToString() => "";
    }
    internal class ParserCache
    {
        public static IParser GetParser(MemberInfo member, string? name = null)
        {
            // We lock on the argument to make sure two threads don't create the same parser
            IParser? parser;
            lock (member) {
                lock (cache) {
                    cache.TryGetValue(member, out parser);
                }
                if (parser == null) {
                    parser = GetParserUncached(member);
                    lock (cache) {
                        cache[member] = parser;
                    }
                }
                if (name == null && !(parser is TraceWrapper)) {
                    lock (wrappers) {
                        if (!wrappers.TryGetValue(parser, out var tw)) {
                            tw = new TraceWrapper(parser);
                            wrappers.Add(parser, tw);
                            return tw;
                        }
                    }
                }
            }
            if (name != null) {
                return new TraceWrapper(parser, name);
            }
            return parser;
        }

        static IParser GetParserUncached(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo field:
                    return ApplyModifiers(field, GetTerminal(member) ?? GetParser(field.FieldType));
                case PropertyInfo property:
                    return ApplyModifiers(property, GetTerminal(member) ?? GetParser(property.PropertyType));
                case Type type:
                    if (type == typeof(string) || type == typeof(Unnamed)) {
                        throw new ArgumentException($"{member} is a string, but has no Terminal attribute");
                    }
                    if (type == typeof(Whitespace)) {
                        return WhitespaceParser.Instance;
                    }
                    if (type == typeof(float)) {
                        return FloatParser.Instance;
                    }
                    if (type == typeof(int)) {
                        return IntParser.Instance;
                    }
                    if (type.IsPrimitive) {
                        throw new NotImplementedException($"{type} not supported yet");
                    }
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                        return new OptionalParser(GetParser(type.GetGenericArguments()[0]));
                    }
                    // TODO: What about normal classes that are ICollections? Maybe a way to override?
                    if (typeof(ICollection).IsAssignableFrom(type)) {
                        return new RepeatParser(type, null);
                    }
                    var tempUnary = new UnaryParser();
                    lock (cache) {
                        cache[member] = tempUnary;
                    }
                    tempUnary.Set(ApplyModifiers(type, (type.IsClass || type.IsValueType) && !type.IsAbstract
                        ? (IParser)new SequenceParser(type)
                        : new AlternativeParser(type)
                    ));
                    return tempUnary.Inner;
                default:
                    throw new ArgumentException();
            }
        }

        static IParser? GetTerminal(MemberInfo member)
        {
            var attr = member.GetCustomAttribute<TerminalAttribute>();
            return attr switch
            {
                null => null,
                LiteralAttribute lit => new LiteralParser(lit),
                RegexAttribute regex => new RegexParser(regex),
                _ => throw new NotSupportedException($"{attr} unimplemented")
            };
        }

        static IParser ApplyModifiers(MemberInfo member, IParser parent)
        {
            if (parent.GetInner() is RepeatParser repeat) {
                parent = RepeatParser.Modify(repeat, member);
            }
            var attrs = member.GetCustomAttributes().ToArray();
            // TODO: Check compiler-generated NullableAttribute (for reference types)
            foreach (var attr in attrs)
            {
                switch (attr)
                {
                    case OptionalAttribute _:
                        parent = new OptionalParser(parent);
                        break;
                    case SurroundByAttribute surround:
                        parent = new SurroundParser(parent, surround);
                        break;
                }
            }
            return parent;
        }

        private static readonly Dictionary<MemberInfo, IParser> cache
            = new Dictionary<MemberInfo, IParser>();

        private static readonly Dictionary<IParser, TraceWrapper> wrappers
            = new Dictionary<IParser, TraceWrapper>();
    }
}