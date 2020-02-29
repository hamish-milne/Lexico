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
        [Term] Pattern pattern;
        [Optional, Literal("$")] string endAnchor;
    }

    public class Alternation
    {
        [SeparatedBy("|")]
        public List<Sequence> Items { get; } = new List<Sequence>();
    }

    public class Sequence
    {
        [Term]
        public List<Pattern> Items { get; } = new List<Pattern>();
    }

    public class Group : Pattern
    {
        [Literal("(")] Unnamed _;
        [Term] GroupModifier modifier;
        [Term] Alternation inner;
        [Literal(")")] Unnamed __;
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
        (char start, char end, bool invert)[] Ranges { get; }
    }

    public abstract class SingleChar : Pattern, ISetItem {
        public abstract char Value { get; }
        public (char start, char end, bool invert)[] Ranges => new []{(Value, Value, false)};

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

        public (char start, char end, bool invert)[] Ranges => new []{(start.Value, end.Value, false)};
    }

    public class CharClass : Pattern, ISetItem
    {
        [Literal("\\")] Unnamed _;
        [CharSet("wWdDsSbB")] char id;

        public (char start, char end, bool invert)[] Ranges => char.ToUpper(id) switch {
            'W' => new []{('a','z'), ('A','Z'), ('0','9'), ('_','_')}, invert: char.IsUpper(id)),
            'D' => new []{('0','9')}, invert: char.IsUpper(id)),
            'S' => new []{('\t','\r'), (' ',' ')}, invert: char.IsUpper(id)),
            'B' => throw new NotSupportedException(),
            _ => throw new InvalidOperationException()
        };
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

    public class Repeat : Pattern
    {
        [Term] Pattern inner;
        [CharSet("*+?")] char repeater;
        [Optional, Literal("?")] string lazy;

        public override IParser Create() => new RepeatParser(inner.Create(), null, min, max);
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

        public override IParser Create() => new CharRange(items.Select(o => o.item.Range).ToArray(), invert: invert != null);
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