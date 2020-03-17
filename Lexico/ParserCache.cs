using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Runtime.CompilerServices;
using static System.AttributeTargets;

namespace Lexico
{
    public delegate IParser ChildParser(MemberInfo? childMember);

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
        public virtual IParser Create(MemberInfo member, ChildParser child, IConfig config) => child(null);

        /// <summary>
        /// Check if the attribute should be implicitly applied to a member
        /// </summary>
        /// <param name="member">The member in question</param>
        /// <returns></returns>
        public virtual bool AddDefault(MemberInfo member) => false;
    }

    internal class UnaryParser : IParser
    {
        private IParser? inner;
        public IParser Inner => inner ?? throw new InvalidOperationException("Circular parser dependency");

        public void Set(IParser inner) {
            if (this.inner != null) {
                throw new InvalidOperationException("Already set");
            }
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Type OutputType => Inner.OutputType;

        public void Compile(ICompileContext context)
        {
            context.Recursive(Inner);
        }

        public override string ToString() => "Unary";
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
            .OrderByDescending(a => a.Priority)
            .ToArray();

        static IParser GetParserUncached(MemberInfo member)
        {
            var defaults = new Queue<TermAttribute>(defaultAttrs);
            var attrs = new Queue<TermAttribute>(member.GetCustomAttributes<TermAttribute>(true).OrderByDescending(a => a.Priority));
            var mConfig = GetConfig(member) ?? Config.Default;
            IParser Next(MemberInfo? child) {
                if (child != null) {
                    if (attrs.Count == 0) {
                        return GetParser(child);
                        // TODO: If this is not true, it is possible for recursion to not work properly
                    }
                    foreach (var attr in child.GetCustomAttributes<TermAttribute>(true).OrderByDescending(a => a.Priority)) {
                        attrs.Enqueue(attr);
                    }
                    member = child;
                    defaults = new Queue<TermAttribute>(defaultAttrs);
                }
                if (attrs.Count > 0) {
                    return attrs.Dequeue().Create(member, Next, mConfig);
                }
                while (defaults.Count > 0) {
                    var d = defaults.Dequeue();
                    if (d.AddDefault(member)) {
                        return d.Create(member, Next, mConfig);
                    }
                }
                // TODO: Put this into TermAttribute.Create instead?
                if (!(member is Type)) {
                    return GetParser(member.GetMemberType());
                }
                throw new ArgumentException($"Incomplete parser definition for {member}");
            }
            var ret = Next(null);
            if (attrs.Count > 0) {
                throw new ArgumentException($"Ambiguous parser definition for {member}; {attrs.Dequeue()} was defined but not used");
            }
            return ret;
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
            static IConfig? GetConfigInternal(MemberInfo? m, IConfig? parent)
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
