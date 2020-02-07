using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Field | Property)]
    public class TermAttribute : Attribute
    {
    }

    internal class UnaryParser : IParser
    {
        public IParser? Inner { get; private set; }

        public void Set(IParser inner) {
            if (Inner != null) {
                throw new InvalidOperationException("Already set");
            }
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Matches(ref IContext context, ref object? value)
        {
            if (Inner == null) {
                throw new InvalidOperationException("Not filled yet");
            }
            return Inner.MatchChild(null, ref context, ref value);
        }

        public override string ToString() => Inner?.ToString() ?? "UNSET";

        public override int GetHashCode() => Inner?.GetHashCode() ?? 0;
        public override bool Equals(object obj) => object.ReferenceEquals(this, obj) || object.ReferenceEquals(Inner, obj);
    }
    internal class ParserCache
    {
        public static IParser GetParser(MemberInfo member)
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

                    try
                    {
                        tempUnary.Set(ApplyModifiers(type, (type.IsClass || type.IsValueType) && !type.IsAbstract
                            ? (IParser)new SequenceParser(type)
                            : new AlternativeParser(type)
                        ));
                        return tempUnary.Inner!;
                    }
                    catch
                    {
                        lock (cache)
                        {
                            cache.Remove(member);
                        }
                        throw;
                    }
                default:
                    throw new ArgumentException();
            }
        }

        static IParser? GetTerminal(MemberInfo member)
        {
            var attr = member.GetCustomAttribute<TerminalAttribute>(true);
            return attr switch
            {
                null => null,
                LiteralAttribute lit => lit.Create(),
                IndirectLiteralAttribute ilit => ilit.Create(member),
                RegexAttribute regex => new RegexParser(regex),
                _ => throw new NotSupportedException($"{attr} unimplemented")
            };
        }

        static IParser ApplyModifiers(MemberInfo member, IParser parent)
        {
            if (parent is UnaryParser u && u.Inner != null) {
                parent = u.Inner;
            }
            if (parent is RepeatParser repeat) {
                parent = RepeatParser.Modify(repeat, member);
            }
            var attrs = member.GetCustomAttributes(true).ToArray();
            // TODO: Check compiler-generated NullableAttribute (for reference types)
            foreach (var attr in attrs)
            {
                parent = attr switch
                {
                    OptionalAttribute _ => new OptionalParser(parent),
                    SurroundByAttribute surround => new SurroundParser(parent, surround),
                    EOFAfterAttribute _ => new EOFParser(parent),
                    _ => parent
                };
            }
            return parent;
        }

        private static readonly Dictionary<MemberInfo, IParser> cache
            = new Dictionary<MemberInfo, IParser>();
    }
}