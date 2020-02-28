using System.Linq;
using System;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace Lexico
{
    public class CharRangeAttribute : TermAttribute
    {
        public CharRangeAttribute(params string[] ranges) {
            this.ranges = ranges.Select(s => {
                if (s.Length != 2) throw new ArgumentException($"Argument `{s}` in CharRange is not 2 characters");
                return (s[0], s[1]);
            }).ToArray();
        }

        private readonly (char, char)[] ranges;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config) => new CharRange(ranges);
    }

    public class CharRange : IParser
    {
        public CharRange((char, char)[] ranges) {
            this.ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        }

        private readonly (char start, char end)[] ranges;

        public Type OutputType => typeof(char);

        public void Compile(ICompileContext context)
        {
            var success = Label();
            foreach (var (start, end) in ranges) {
                context.Append(IfThen(And(
                    GreaterThanOrEqual(context.Peek(0), Constant(start)),
                    LessThanOrEqual(context.Peek(0), Constant(end))
                ), Goto(success)));
            }
            context.Fail();
            context.Append(Label(success));
            context.Succeed(context.Peek(0));
        }
    }
}