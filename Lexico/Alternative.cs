using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Lexico
{
    /// <summary>
    /// Matches the first non-abstract class that is assignable to the member.
    /// Applied by default to abstract classes and interfaces.
    /// </summary>
    public class AlternativeAttribute : TermAttribute
    {
        // ReSharper disable once UnusedMember.Global - Required as Activator.CreateInstance cannot use params constructors to create a parameterless instance
        public AlternativeAttribute() { Options = null; }
        public AlternativeAttribute(params Type[] options) { Options = options; }
        public Type[]? Options { get; }
        public override int Priority => 10;
        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) =>
            new AlternativeParser(member.GetMemberType(), Options);

        public override bool AddDefault(MemberInfo member)
            => member is Type t && (t.IsInterface || t.IsAbstract);
    }

    public class IndirectAlternativeAttribute : TerminalAttribute
    {
        public IndirectAlternativeAttribute(string property) {
            Property = property ?? throw new ArgumentNullException(nameof(property));
        }
        public string Property { get; }

        public override IParser Create(MemberInfo member, IConfig config)
        {
            var prop = member.ReflectedType.GetProperty(Property, Instance | Public | NonPublic)
                       ?? throw new ArgumentException($"Could not find `{Property}` on {member.ReflectedType}");
            return prop.GetValue(Activator.CreateInstance(member.ReflectedType, true)) is IEnumerable<Type> options
                       ? new AlternativeParser(member.GetMemberType(), options)
                       : throw new ArgumentNullException($"Found `{Property}` is not IEnumerable<Type>");
        }
    }

    internal class AlternativeParser : IParser
    {
        public AlternativeParser(Type baseType, IEnumerable<Type>? optionTypes = null)
        {
            OutputType = baseType;
            if (optionTypes != null)
            {
                foreach (var type in optionTypes)
                {
                    if (!baseType.IsAssignableFrom(type)) throw new ArgumentException($"Option '{type}' is not assignable to base type '{baseType}'");
                }
            }
            else
            {
                optionTypes = baseType.Assembly.GetTypes().Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && baseType.IsAssignableFrom(t));
            }

            options = optionTypes.Select(ParserCache.GetParser).ToArray();
            if (options.Length == 0)
            {
                throw new ArgumentException($"{baseType} has no concrete options");
            }
        }

        public AlternativeParser(Type outputType, IEnumerable<IParser> options)
        {
            OutputType = outputType;
            this.options = options.ToArray();
        }

        private readonly IParser[] options;

        public Type OutputType { get; }

        public void Compile(ICompileContext context)
        {
            foreach (var option in options)
            {
                var savePoint = context.Save();
                context.Child(option, null, context.Result, context.Success, savePoint);
                context.Restore(savePoint);
            }
            context.Fail();
        }

        public override string ToString() => $"Any {OutputType.Name}";
    }
}