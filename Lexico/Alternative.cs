using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Reflection.BindingFlags;
using static System.Linq.Expressions.Expression;

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
            new AlternativeParser(member.GetMemberType(), config, ParserFlags, Options);

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
            if (ReflectedType == null) {
                throw new Exception("'Indirect' attributes must be applied to a class member");
            }
            var prop = ReflectedType.GetProperty(Property, Instance | Public | NonPublic)
                       ?? throw new ArgumentException($"Could not find `{Property}` on {ReflectedType}");
            return prop.GetValue(Activator.CreateInstance(ReflectedType, true)) is IEnumerable<Type> options
                       ? new AlternativeParser(member.GetMemberType(), config, ParserFlags, options)
                       : throw new ArgumentNullException($"Found `{Property}` is not IEnumerable<Type>");
        }
    }

    internal class AlternativeParser : ParserBase
    {
        public AlternativeParser(Type baseType, IConfig config, ParserFlags flags, IEnumerable<Type>? optionTypes = null, IEnumerable<Type>? exclude = null) : base(config, flags)
        {
            OutputType = baseType;
            if (optionTypes != null)
            {
                optionTypes = optionTypes.ToArray();
                if (baseType != typeof(Unnamed))
                    foreach (var type in optionTypes)
                    {
                        if (!baseType.IsAssignableFrom(type)) throw new ArgumentException($"Option '{type}' is not assignable to base type '{baseType}'");
                    }
            }
            else
            {
                optionTypes = baseType.Assembly.GetTypes().Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && baseType.IsAssignableFrom(t));
            }

            // ReSharper disable once PossibleMultipleEnumeration
            var toExclude = new HashSet<Type>(exclude ?? Array.Empty<Type>());
            _options = optionTypes.Where(t => !toExclude.Contains(t)).Select(ParserCache.GetParser).ToArray();
            if (_options.Length == 0)
            {
                throw new ArgumentException($"{baseType} has no concrete options");
            }
        }

        public AlternativeParser(Type outputType, IConfig config, ParserFlags flags, IEnumerable<IParser> alternatives) : base(config, flags)
        {
            OutputType = outputType;
            _options = alternatives.ToArray();
        }

        private readonly IParser[] _options;

        public override Type OutputType { get; }

        public override void Compile(Context context)
        {
            var e = context.Emitter;
            var success = context.Success ?? e.Label();
            // var cut = e.Var(false, typeof(bool));
            foreach (var option in _options)
            {
                var savePoint = context.Save();
                context.Child(option, null, context.Result, success, savePoint.label);
                context.Restore(savePoint);
                // context.Append(IfThen(cut, Goto(context.Failure)));
            }
            context.Fail();
            if (context.Success == null) {
                e.Mark(success);
            }
        }

        public override string ToString() => $"Any {OutputType.Name}";
    }

    public class CutAttribute : TermAttribute
    {
        public override int Priority => 200;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new CutParser(child(null), config, ParserFlags);
    }

    internal class CutParser : ParserBase
    {
        private readonly IParser previous;
        public CutParser(IParser previous, IConfig config, ParserFlags parserFlags) : base(config, parserFlags)
        {
            this.previous = previous;
        }

        public override Type OutputType => previous.OutputType;

        public override void Compile(Context context)
        {
            context.Succeed();
            // if (context.Cut == null) {
            //     previous.Compile(context);
            // } else {
            //     context.Child(previous, null, context.Result, null, context.Failure);
            //     context.Append(Assign(context.Cut, Constant(true)));
            //     context.Succeed();
            // }
        }
    }
}