using System.Linq;
using System;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace Lexico
{
    public class CharSetAttribute : TermAttribute
    {
        public CharSetAttribute(string set) {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        private readonly string set;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config) => new CharSet(set);
    }

    public class CharSet : IParser
    {
        public CharSet(string set) {
            set = set ?? throw new ArgumentNullException(nameof(set));
            chars = set.Distinct().ToArray();
            if (chars.Length == 0) {
                throw new ArgumentException("Char set is empty");
            }
        }

        public Type OutputType => typeof(char);

        private readonly char[] chars;

        public void Compile(ICompileContext context)
        {
            var success = Label();
            foreach (var c in chars) {
                context.Append(IfThen(Equal(context.Peek(0), Constant(c)), Goto(success)));
            }
            context.Fail();
            context.Append(Label(success));
            context.Succeed(context.Peek(0));
        }
    }
}