using System.Linq;
using System;
using System.Reflection;

namespace Lexico
{
    /// <summary>
    /// Matches the first non-abstract class that is assignable to the member.
    /// Applied by default to abstract classes and interfaces.
    /// </summary>
    public class AlternativeAttribute : TermAttribute
    {
        public override int Priority => 10;
        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
        {
            return new AlternativeParser(member.GetMemberType());
        }

        public override bool AddDefault(MemberInfo member)
            => member is Type t && (t.IsInterface || t.IsAbstract);
    }

    internal class AlternativeParser : IParser
    {
        public AlternativeParser(Type baseType)
        {
            this.baseType = baseType;
            options = baseType.Assembly.GetTypes()
                .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && baseType.IsAssignableFrom(t))
                .Select(t => ParserCache.GetParser(t))
                .ToArray();
            if (options.Length == 0) {
                throw new ArgumentException($"{baseType} has no concrete options");
            }
        }

        private readonly Type baseType;
        private readonly IParser[] options;

        public Type OutputType => baseType;

        public void Compile(ICompileContext context)
        {
            foreach (var option in options)
            {
                var savePoint = context.Save();
                context.Child(option, context.Result, context.Success, savePoint);
                context.Restore(savePoint);
            }
            context.Fail();
        }

        public override string ToString() => $"Any {baseType.Name}";
    }
}