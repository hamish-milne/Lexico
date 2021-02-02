#pragma warning disable CS8618,IDE0044,IDE0051,CS0169,CS0649
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace Lexico.RegexImpl
{
    public abstract class Pattern
    {
        public abstract IParser Create();
    }

    [CompileFlags(CompileFlags.CheckImmediateLeftRecursion | CompileFlags.Memoizing)]
    [TopLevel]
    public class Regex
    {
        [Optional, Literal("^")] string beginAnchor;
        [Term] Alternation pattern;
        [Optional, Literal("$")] string endAnchor;

        public IParser Create() => new SubstringParser(endAnchor == null
            ? pattern.Create()
            : new ConcatParser(pattern.Create(), EOFParser.Instance));

        public static IParser Parse(string pattern) => Lexico.Parse<Regex>(pattern).Create();
    }

    public class SubstringParser : IParser
    {
        public SubstringParser(IParser inner)
        {
            this.inner = inner;
        }

        private readonly IParser inner;

        public Type OutputType => typeof(string);

        public void Compile(Context context)
        {
            context.PopCachedResult();
            var e = context.Emitter;
            var start = context.GetFeature<StartPosition>().Get();
            context.Child(inner, null, ResultMode.None, null, context.Failure);
            if (context.HasResult()) {
                e.Load(context.Sequence);
                e.Load(start);
                e.Load(context.Position);
                e.Load(start);
                e.Operate(BOP.Subtract);
                e.Call(typeof(string).GetMethod(nameof(string.Substring)));
            } else {
                context.Succeed();
            }
        }
    }

    class ConcatParser : IParser
    {
        public ConcatParser(params IParser[] children)
        {
            this.children = children;
        }

        public Type OutputType => typeof(void);

        private readonly IParser[] children;

        public void Compile(Context context)
        {
            foreach (var c in children)
            {
                context.Child(c, null, ResultMode.None, null, context.Failure);
            }
            context.Succeed();
        }

        public override string ToString() => "Regex sequence";
    }

    public class Alternation
    {
        [SeparatedBy("|")]
        public List<Sequence> Items { get; } = new List<Sequence>();

        public IParser Create() => Items.Count == 1 ? Items[0].Create()
            : new AlternativeParser(typeof(void), Items.Select(s => s.Create()));
    }

    public class Sequence
    {
        [Term]
        public List<Pattern> Items { get; } = new List<Pattern>();

        public IParser Create() => Items.Count == 1 ? Items[0].Create()
            : new ConcatParser(Items.Select(s => s.Create()).ToArray());
    }

    public class Repeat : Pattern
    {
        [Term] Pattern inner;
        [CharSet("*+")] char repeater;
        [Optional, Literal("?")] string lazy; // TODO: Support 'lazy' qualifiers

        public override IParser Create() => new RepeatParser(typeof(string), inner.Create(), null, repeater == '+' ? 1 : 0, null);
    }

    public class Optional : Pattern
    {
        [Term] Pattern inner;
        [Literal("?")] Unnamed _;

        public override IParser Create() => new OptionalParser(inner.Create());
    }

    public class Group : Pattern
    {
        [Literal("(")] Unnamed _;
        [Optional] GroupModifier? modifier;
        [Term] Alternation inner;
        [Literal(")")] Unnamed __;

        public override IParser Create() => modifier?.Modify(inner.Create()) ?? inner.Create();
    }

    public abstract class GroupModifier
    {
        public virtual IParser Modify(IParser input) => input;
    }

    public class NonCapturing : GroupModifier
    {
        [Literal("?")] Unnamed _;
        [CharSet(":>")] Unnamed __;
    }

    public class Lookaround : GroupModifier
    {
        [Literal("?")] Unnamed _;
        [Optional, Literal("<")] string? behind;
        [CharSet("=!")] char direction;

        public override IParser Modify(IParser input)
        {
            if (behind != null) {
                throw new NotSupportedException("Look-behind not supported");
            }
            if (direction == '!') {
                return new NotParser(input);
            } else {
                return new LookAheadParser(input);
            }
        }
    }

    public class Named : GroupModifier
    {
        [SurroundBy("?<", ">"), Repeat(Min = 1), CharRange("09", "AZ", "az", "__")]
        string name;
    }

    public interface ISetItem
    {
        CharIntervalSet Ranges { get; }
    }

    public class CharRangePattern : ISetItem
    {
        [Term] SingleChar start;
        [Literal("-")] Unnamed _;
        [Term] SingleChar end;

        public CharIntervalSet Ranges => new CharIntervalSet().Include(start.Value, end.Value);
    }

    public abstract class SingleChar : Pattern, ISetItem
    {
        public abstract char Value { get; }
        public CharIntervalSet Ranges => new CharIntervalSet().Include(Value, Value);

        public override IParser Create()
        {
            return new CharSet(new CharIntervalSet().Include(Value, Value));
        }
    }

    public class HexChar : SingleChar
    {
        [Literal("\\x")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 2, Max = 2)] string hexValue;
        public override char Value => (char)byte.Parse(hexValue, NumberStyles.AllowHexSpecifier);
    }

    public class ControlChar : SingleChar
    {
        [Literal("\\c")] Unnamed _;
        [CharRange("AZ")] char id;
        public override char Value => (char)(id - 'A' + 1);
    }

    public class ExtendedUnicodeChar : SingleChar
    {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 0)] string hexValue;
        public override char Value => (char)uint.Parse(hexValue, NumberStyles.AllowHexSpecifier);
    }

    public class UnicodeChar : SingleChar
    {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 4, Max = 4)] string hex;
        public override char Value => (char)ushort.Parse(hex, NumberStyles.AllowHexSpecifier);
    }

    public class OctalChar : SingleChar
    {
        [Literal("\\")] Unnamed _;
        [CharRange("07"), Repeat(Min = 3, Max = 3)] string octal;
        public override char Value => (char)((octal[0] - '0') * 8 * 8 + (octal[1] - '0') * 8 + (octal[2] - '0'));
    }

    public class NumericRef : Pattern
    {
        [Literal("\\")] Unnamed _;
        [CharRange("19")] char number;

        public override IParser Create() => throw new NotSupportedException();
    }

    public class WordBoundary : Pattern
    {
        [Literal("\\b")] Unnamed _;

        public override IParser Create()
        {
            throw new NotImplementedException();
        }
    }

    public class CharClass : Pattern, ISetItem
    {
        [Literal("\\")] Unnamed _;
        [CharSet("wWdDsS")] char id;

        // TODO: C# extensions like \p{}? https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference
        public CharIntervalSet Ranges
        {
            get
            {
                var set = char.ToLowerInvariant(id) switch
                {
                    'w' => new CharIntervalSet().Include('a', 'z').Include('A', 'Z').Include('0', '9').Include('_', '_'),
                    'd' => new CharIntervalSet().Include('0', '9'),
                    's' => new CharIntervalSet().Include('\t', '\r').Include(' ', ' '),
                    _ => throw new InvalidOperationException()
                };
                if (char.IsUpper(id))
                {
                    set.Invert();
                }
                return set;
            }
        }

        public override IParser Create() => new CharSet(Ranges);
    }

    public class SimpleEscape : SingleChar
    {
        [Literal("\\")] Unnamed _;
        [Term] char c;
        public override char Value => c switch
        {
            't' => '\t',
            'n' => '\n',
            'v' => '\v',
            'f' => '\f',
            'r' => '\r',
            '0' => '\0',
            '\\' => '\\',
            _ => c
        };
    }

    public class Quantified : Pattern
    {
        [Term] Pattern inner;
        [Literal("{")] Unnamed _;

        // TODO: make a regex-less Number 
        [CharRange("09"), Repeat] string min;
        [Literal(","), Optional] Unnamed __;
        [Optional, CharRange("09"), Repeat] string max;
        [Literal("}")] Unnamed ___;

        public override IParser Create() => new RepeatParser(typeof(string), inner.Create(), null,
            int.Parse(min), max == null ? default(int?) : int.Parse(max));
    }

    public struct SetItem
    {
        [Not, CharSet("]")] Unnamed _;
        [Term] public ISetItem item;
    }

    public class Set : Pattern
    {
        [Literal("[")] Unnamed _;
        [Optional, Literal("^")] string invert;
        [Term] List<SetItem> items; // TODO: Min 1 or 0 here?
        [Literal("]")] Unnamed __;

        public override IParser Create()
        {
            var set = new CharIntervalSet(items.Select(i => i.item.Ranges));
            if (invert != null)
            {
                set.Invert();
            }
            return new CharSet(set);
        }
    }

    public class Any : Pattern
    {
        [Literal(".")] Unnamed _;

        public override IParser Create() => CharParser.Instance;
    }

    public class RawChar : SingleChar
    {
        [Not, CharSet(")|")] Unnamed _;
        [CharRange("\x20\uffff")] char value;
        public override char Value => value;
    }
}