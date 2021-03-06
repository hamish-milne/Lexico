#pragma warning disable CS8618,IDE0044,IDE0051,CS0169,CS0649
using System.Linq.Expressions;
using System.Text;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using static System.Linq.Expressions.Expression;

namespace Lexico.RegexImpl
{
    public abstract class Pattern
    {
        public abstract IParser Create(IConfig config, ParserFlags flags);
    }

    [CompileFlags(CompileFlags.CheckImmediateLeftRecursion | CompileFlags.Memoizing)]
    [TopLevel]
    public class Regex
    {
        [Optional, Literal("^")] string beginAnchor;
        [Term] Alternation pattern;
        [Optional, Literal("$")] string endAnchor;

        public class Parser : ParserBase
        {
            private readonly IParser _inner;
            private readonly string _pattern;

            public Parser(IParser inner, string pattern, IConfig config,  ParserFlags flags) : base(config, flags)
            {
                _inner = inner;
                _pattern = pattern;
            }

            public override Type OutputType => _inner.OutputType;
            public override void Compile(ICompileContext context) => _inner.Compile(context);
            public override string ToString() => $"Regex pattern: {_pattern}";
        }

        public IParser Create(string regexPattern, IConfig config, ParserFlags flags) => new Parser(new SubstringParser(endAnchor == null
                                                                                                                             ? pattern.Create(config, flags)
                                                                                                                             : new ConcatParser(config, flags, pattern.Create(config, flags), new EOFParser(config, flags)),
                                                                                                                        config,
                                                                                                                        flags),
                                                                                                     regexPattern, config, flags); // TODO: Don't pass in regexPattern, retrieve it by unparsing

        public static IParser Parse(string pattern, IConfig config, ParserFlags flags) => Lexico.Parse<Regex>(pattern).Create(pattern, config, flags);
    }

    public class SubstringParser : ParserBase
    {
        public SubstringParser(IParser inner, IConfig config, ParserFlags flags) : base(config, flags) => this._inner = inner;

        private readonly IParser _inner;

        public override Type OutputType => typeof(string);

        public override void Compile(ICompileContext context)
        {
            var start = context.Cache(context.Position);
            context.Child(_inner, null, null, null, context.Failure);
            context.Succeed(Call(context.String, nameof(string.Substring), Type.EmptyTypes, start, Subtract(context.Position, start)));
            context.Release(start);
        }
    }

    public class ConcatParser : ParserBase
    {
        public ConcatParser(IConfig config, ParserFlags flags, params IParser[] children) : base(config, flags) => this._children = children;

        public override Type OutputType => typeof(string);

        private readonly IParser[] _children;

        public override void Compile(ICompileContext context)
        {
            Expression? sb = null;
            if (context.Result != null)
            {
                sb = context.Cache(New(typeof(StringBuilder)));
            }
            foreach (var c in _children)
            {
                Expression? output = null;
                if (sb != null)
                {
                    output = context.Cache(Default(c.OutputType == typeof(char) ? typeof(char?) : typeof(string)));
                }
                context.Child(c, null, output, null, context.Failure);
                if (sb != null)
                {
                    context.Append(Call(sb, nameof(StringBuilder.Append), Type.EmptyTypes, Convert(output, typeof(object))));
                }
            }
            if (sb != null)
            {
                context.Succeed(Call(sb, nameof(StringBuilder.ToString), Type.EmptyTypes));
            }
            else
            {
                context.Succeed();
            }
            context.Release(sb);
        }

        public override string ToString() => "Regex sequence";
    }

    public class Alternation
    {
        [SeparatedBy("|")]
        public List<Sequence> Items { get; } = new List<Sequence>();

        public IParser Create(IConfig config, ParserFlags flags) => Items.Count == 1 ? Items[0].Create(config, flags)
                                                                       : new AlternativeParser(typeof(string), config, flags, Items.Select(s => s.Create(config, flags)));
    }

    public class Sequence
    {
        [Term]
        public List<Pattern> Items { get; } = new List<Pattern>();

        public IParser Create(IConfig config, ParserFlags flags) => Items.Count == 1 ? Items[0].Create(config, flags)
                                                            : new ConcatParser(config, flags, Items.Select(s => s.Create(config, flags)).ToArray());
    }

    public class Repeat : Pattern
    {
        [Term] Pattern inner;
        [CharSet("*+")] char repeater;
        [Optional, Literal("?")] string lazy; // TODO: Support 'lazy' qualifiers

        public override IParser Create(IConfig config, ParserFlags flags) => new RepeatParser(typeof(string), inner.Create(config, flags), null, repeater == '+' ? 1 : 0, null, config, flags);
    }

    public class Optional : Pattern
    {
        [Term] Pattern inner;
        [Literal("?")] Unnamed _;

        public override IParser Create(IConfig config, ParserFlags flags) => new OptionalParser(inner.Create(config, flags), config, flags);
    }

    public class Group : Pattern
    {
        [Literal("(")] Unnamed _;
        [Optional] GroupModifier? modifier;
        [Term] Alternation inner;
        [Literal(")")] Unnamed __;

        public override IParser Create(IConfig config, ParserFlags flags) => modifier?.Modify(inner.Create(config, flags), config, flags) ?? inner.Create(config, flags);
    }

    public abstract class GroupModifier
    {
        public virtual IParser Modify(IParser input, IConfig config, ParserFlags parserFlags) => input;
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

        public override IParser Modify(IParser input, IConfig config, ParserFlags parserFlags)
        {
            if (behind != null) {
                throw new NotSupportedException("Look-behind not supported");
            }
            if (direction == '!') {
                return new NotParser(input, config, parserFlags);
            } else {
                return new LookAheadParser(input, config, parserFlags);
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

        public override IParser Create(IConfig config, ParserFlags flags) => new CharSet(new CharIntervalSet().Include(Value, Value), config, flags);
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

        public override IParser Create(IConfig config, ParserFlags flags) => throw new NotSupportedException();
    }

    public class WordBoundary : Pattern
    {
        [Literal("\\b")] Unnamed _;

        public override IParser Create(IConfig config, ParserFlags flags)
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

        public override IParser Create(IConfig config, ParserFlags flags) => new CharSet(Ranges, config, flags);
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

        public override IParser Create(IConfig config, ParserFlags flags) => new RepeatParser(typeof(string), inner.Create(config, flags), null,
                                                                                              int.Parse(min), max == null ? default(int?) : int.Parse(max), config, flags);
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

        public override IParser Create(IConfig config, ParserFlags flags)
        {
            var set = new CharIntervalSet(items.Select(i => i.item.Ranges));
            if (invert != null)
            {
                set.Invert();
            }
            return new CharSet(set, config, flags);
        }
    }

    public class Any : Pattern
    {
        [Literal(".")] Unnamed _;

        public override IParser Create(IConfig config, ParserFlags flags) => new CharParser(config, flags);
    }

    public class RawChar : SingleChar
    {
        [Not, CharSet(")|")] Unnamed _;
        [CharRange("\x20\uffff")] char value;
        public override char Value => value;
    }
}