#pragma warning disable CS8618,IDE0044,IDE0051
using System;
using System.Linq;
using System.Globalization;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace Lexico.RegexImpl
{
    public abstract class Pattern
    {
        public abstract IParser Create();
    }

    public class Regex
    {
        [Optional, Literal("^")] string beginAnchor;
        [Term] Alternation pattern;
        [Optional, Literal("$")] string endAnchor;

        public IParser Create() => endAnchor == null
            ? pattern.Create()
            : new SequenceParser(new []{pattern.Create(), EOFParser.Instance});
    }

    public class Alternation
    {
        [SeparatedBy("|")]
        public List<Sequence> Items { get; } = new List<Sequence>();

        public IParser Create() => Items.Count == 1 ? Items[0].Create() : new AlternativeParser(Items.Select(s => s.Create()));
    }

    public class Sequence
    {
        [Term]
        public List<Pattern> Items { get; } = new List<Pattern>();

        public IParser Create() => Items.Count == 1 ? Items[0].Create() : new SequenceParser(Items.Select(s => s.Create()));
    }

    public class Group : Pattern
    {
        [Literal("(")] Unnamed _;
        [Term] GroupModifier modifier;
        [Term] Alternation inner;
        [Literal(")")] Unnamed __;

        public override IParser Create() => inner.Create();
    }

    public abstract class GroupModifier {}

    public class NonCapturing : GroupModifier {
        [Literal("?:")] Unnamed _;
    }

    public class Lookaround : GroupModifier {
        [Literal("?")] Unnamed _;
        [Optional, Literal("<")] string? behind;
        [CharSet("=!")] char direction;
    }

    public class Named : GroupModifier {
        [SurroundBy("?<", ">"), Repeat(Min = 1), CharRange("09", "AZ", "az", "__")]
        string name;
    }

    public interface ISetItem {
        CharIntervalSet Ranges { get; }
    }

    public abstract class SingleChar : Pattern, ISetItem {
        public abstract char Value { get; }
        public CharIntervalSet Ranges => new CharIntervalSet().Include(Value, Value);

        public override IParser Create()
        {
            return new CharSet(Value.ToString());
        }
    }

    public class HexChar : SingleChar {
        [Literal("\\x")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 2, Max = 2)] string hexValue;
        public override char Value => (char)byte.Parse(hexValue, NumberStyles.AllowHexSpecifier);
    }

    public class ControlChar : SingleChar {
        [Literal("\\c")] Unnamed _;
        [CharRange("AZ")] char id;
        public override char Value => (char)(id - 'A' + 1);
    }

    public class UnicodeChar : SingleChar {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 4, Max = 4)] string hex;
        public override char Value => (char)ushort.Parse(hex, NumberStyles.AllowHexSpecifier);
    }

    public class OctalChar : SingleChar {
        [Literal("\\")] Unnamed _;
        [CharRange("07"), Repeat(Min = 3, Max = 3)] string octal;
        public override char Value => (char)((octal[0]-'0')*8*8 + (octal[1]-'0')*8 + (octal[2]-'0'));
    }

    public class NumericRef : Pattern {
        [Literal("\\")] Unnamed _;
        [CharRange("19")] char number;

        public override IParser Create() => throw new NotSupportedException();
    }

    public class ExtendedUnicodeChar : SingleChar
    {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af"), Repeat(Min = 0)] string hexValue;
        public override char Value => (char)uint.Parse(hexValue, NumberStyles.AllowHexSpecifier);
    }

    public class CharRangePattern : ISetItem
    {
        [Term] SingleChar start;
        [Literal("-")] Unnamed _;
        [Term] SingleChar end;

        public CharIntervalSet Ranges => new CharIntervalSet().Include(start.Value, end.Value);
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

        public CharIntervalSet Ranges
        {
            get {
                var set = char.ToLowerInvariant(id) switch {
                    'w' => new CharIntervalSet().Include('a','z').Include('A','Z').Include('0','9').Include('_','_'),
                    'd' =>  new CharIntervalSet().Include('0','9'),
                    's' => new CharIntervalSet().Include('\t','\r').Include(' ',' '),
                    _ => throw new InvalidOperationException()
                };
                if (char.IsUpper(id)) {
                    set.Invert();
                }
                return set;
            }
        }

        public override IParser Create()
        {
            throw new NotImplementedException();
        }
    }

    public class SimpleEscape : SingleChar
    {
        [Literal("\\")] Unnamed _;
        [Term] char c;
        public override char Value => c switch {
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

    public class Optional : Pattern
    {
        [Term] Pattern inner;
        [Literal("?")] Unnamed _;

        public override IParser Create() => new OptionalParser(inner.Create());
    }

    public class Repeat : Pattern
    {
        [Term] Pattern inner;
        [CharSet("*+")] char repeater;
        [Optional, Literal("?")] string lazy; // TODO: Support 'lazy' qualifiers

        public override IParser Create() => new RepeatParser(inner.Create(), null, repeater == '+' ? 1 : 0);
    }

    public class Quantified : Pattern
    {
        [Term] Pattern inner;
        [Literal("{")] Unnamed _;
        [Term] int min;
        [Literal(","), Optional] Unnamed __;
        [Term] int? max;
        [Literal("}")] Unnamed ___;

        public override IParser Create() => new RepeatParser(inner.Create(), null, min, max);
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
        [Term] List<SetItem> items;
        [Literal("]")] Unnamed __;

        public override IParser Create() {
            var set = new CharIntervalSet(items.Select(i => i.item.Ranges));
            if (invert != null) {
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

    public class RawChar : SingleChar {
        [CharRange("\x32\uffff")] char value;
        public override char Value => value;
    }
}