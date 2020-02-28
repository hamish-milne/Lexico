using System.Linq.Expressions;
using System.Linq;
using System.Collections.Generic;
using System;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    public class CharSetAttribute : TermAttribute
    {

    }

    public class CharSet : IParser
    {
        public CharSet(string set) {
            chars = set.Distinct().ToArray();
            if (chars.Length == 0) {
                throw new ArgumentException("Char set is empty");
            }
        }

        public Type OutputType => typeof(char);

        private readonly char[] chars;

        public void Compile(ICompileContext context)
        {
            var success = context.Success ?? Label();
            foreach (var c in chars) {
                context.Append(IfThen(Equal(context.Peek(0), Constant(c)), Goto(success)));
            }
            context.Fail();
            if (context.Success == null) {
                context.Append(Label(success));
            }
        }
    }

    public class CharRange : IParser
    {
        public 
    }
}