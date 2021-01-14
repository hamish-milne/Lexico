using System.Text.RegularExpressions;
using System;
using System.Reflection;

namespace Lexico
{
    /// <summary>
    /// Matches any amount of whitespace (at least one character).
    /// New-lines do not count by default; see the MultiLine attribute
    /// </summary>
    public class WhitespaceAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config)
            => new WhitespaceParser(config);
    }

    /// <summary>
    /// Matches any amount of whitespace (at least one character).
    /// New-lines do not count by default; see the MultiLine attribute
    /// </summary>
    [Whitespace] public struct Whitespace {}

    internal class WhitespaceParser : IParser
    {
        public WhitespaceParser(IConfig config) {
            multiline = (config.Get<RegexOptions>(default) & RegexOptions.Multiline) != 0;
        }

        private readonly bool multiline;

        public Type OutputType => typeof(void);

        public void Compile(Context context)
        {
            var e = context.Emitter;
            var space = e.Const(' ');
            var nl = e.Const('\n');
            // TODO: Opt-in for fast whitespace
            Action<Label> multilineTest = label => {
                e.Compare(context.Position, CompareOp.GreaterOrEqual, context.Length, label);
                e.Compare(context.Peek(0), CompareOp.Greater, space, label);
            };
            Action<Label> test;
            if (multiline) {
                test = multilineTest;
            } else {
                test = label => { multilineTest(label); e.Compare(context.Peek(0), CompareOp.Equal, nl, label); };
            }
            // var isWhiteSpace = new Func<char, bool>(Char.IsWhiteSpace).Method;
            // Expression test = Call(isWhiteSpace, context.Peek(0));
            
            test(context.Failure);
            var loop = e.Label();
            var loopEnd = context.Success ?? e.Label();
            e.Mark(loop);
            context.Advance(1);
            test(loopEnd);
            if (context.Success == null) {
                e.Mark(loopEnd);
            }
        }

        public override string ToString() => "Whitespace";
    }
}