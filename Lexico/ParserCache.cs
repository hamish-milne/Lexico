using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using static System.AttributeTargets;

namespace Lexico
{
    /// <summary>
    /// Apply to a Member to include it in the parent parser. Generates Parser implementations for members it is applied to.
    /// </summary>
    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = true)]
    public class TermAttribute : Attribute
    {
        /// <summary>
        /// Determines the order that parsers are generated in. Higher priority attributes are evaluated first,
        /// and therefore applied last in the chain.
        /// </summary>
        public virtual int Priority => -10;

        /// <summary>
        /// Creates a Parser based on this attribute
        /// </summary>
        /// <param name="member">The Member (Field, Property or Type) the attribute was applied to</param>
        /// <param name="child">Call this function to get the previous parser in the chain</param>
        /// <param name="config">Allows getting contextual configuration values
        /// (typically from attributes on the member or containing type)</param>
        /// <returns>The constructed Parser</returns>
        public virtual IParser Create(MemberInfo member, Func<IParser> child, IConfig config) => child();

        /// <summary>
        /// Check if the attribute should be implicitly applied to a member
        /// </summary>
        /// <param name="member">The member in question</param>
        /// <returns></returns>
        public virtual bool AddDefault(MemberInfo member) => false;
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

    public static class ReflectionExtensions
    {
        public static Type GetMemberType(this MemberInfo member)
            => member switch
            {
                FieldInfo f => f.FieldType,
                PropertyInfo p => p.PropertyType,
                Type t => t,
                null => throw new ArgumentNullException(nameof(member)),
                _ => throw new ArgumentException($"{member} is invalid")
            };
    }

    internal class ParserCache
    {
        private static readonly Stack<MemberInfo> parserStack = new Stack<MemberInfo>();

        private static readonly Dictionary<MemberInfo, IParser> cache
            = new Dictionary<MemberInfo, IParser>();

        public static IParser GetParser(MemberInfo member)
        {
            // We need a global lock to detect recursion
            lock (parserStack) {
                if (!cache.TryGetValue(member, out var parser)) {
                    if (parserStack.Contains(member)) {
                        var placeholder = new UnaryParser();
                        cache.Add(member, placeholder);
                        return placeholder;
                    } else {
                        parserStack.Push(member);
                        parser = GetParserUncached(member);
                        parserStack.Pop();
                        if (cache.TryGetValue(member, out var tmp) && tmp is UnaryParser placeholder) {
                            placeholder.Set(parser);
                        }
                        cache[member] = parser;
                    }
                }
                return parser;
            }
        }

        private static readonly TermAttribute[] defaultAttrs = typeof(TermAttribute)
            .Assembly.GetExportedTypes()
            .Where(typeof(TermAttribute).IsAssignableFrom)
            .Select(t => { try { return Activator.CreateInstance(t); } catch { return null; }}) // TODO: Report errors here instead of throwing them away
            .OfType<TermAttribute>()
            .OrderBy(a => a.Priority)
            .ToArray();

        static IParser GetParserUncached(MemberInfo member)
        {
            var defaults = new Stack<TermAttribute>(defaultAttrs);
            var attrs = new Stack<TermAttribute>(member.GetCustomAttributes<TermAttribute>(true).OrderBy(a => a.Priority));
            var mConfig = GetConfig(member) ?? Config.Default;
            IParser Next() {
                if (attrs.Count > 0) {
                    return attrs.Pop().Create(member, Next, mConfig);
                }
                while (defaults.Count > 0) {
                    var d = defaults.Pop();
                    if (d.AddDefault(member)) {
                        return d.Create(member, Next, mConfig);
                    }
                }
                if (!(member is Type)) {
                    return GetParser(member.GetMemberType());
                }
                throw new ArgumentException($"Incomplete parser definition for {member}");
            }
            return Next();
        }

        private class Config : IConfig
        {
            public static Config Default { get; } = new Config(Array.Empty<IConfigBase>(), null);
            public Config(IConfigBase[] attributes, IConfig? parent) {
                this.parent = parent;
                this.attributes = attributes;
            }
            private readonly IConfig? parent;
            private readonly IConfigBase[] attributes;

            public T Get<T>(T defaultValue)
            {
                return attributes.OfType<IConfig<T>>()
                #pragma warning disable 8604
                    .Aggregate(parent == null ? defaultValue : parent.Get(defaultValue), (value, conf) => {
                        conf.ApplyConfig(ref value);
                        return value;
                    });
            }
        }

        private static IConfig? GetConfig(MemberInfo member)
        {
            IConfig? GetConfigInternal(MemberInfo? m, IConfig? parent)
            {
                if (m == null) {
                    return parent;
                }
                var attrs = m.GetCustomAttributes().OfType<IConfigBase>().ToArray();
                if (attrs.Length == 0) {
                    return parent;
                }
                return new Config(attrs, parent);
            }
            return GetConfigInternal(member, GetConfigInternal(member.ReflectedType,
                member is Type ? null : GetConfigInternal(member.GetMemberType(), null)));
        }
    }
}
