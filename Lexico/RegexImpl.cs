using System;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace Lexico.Regex
{
    public abstract class Pattern {}

    public class Regex
    {
        [Optional, Literal("^")] string beginAnchor;
        [Term] Pattern pattern;
        [Optional, Literal("$")] string endAnchor;
    }

    public class Alternation : Pattern
    {
        [SeparatedBy("|")]
        public List<Pattern> Items { get; } = new List<Pattern>();
    }

    public class Group : Pattern
    {
        [SurroundBy("(", ")")]
        public List<Pattern> Items { get; } = new List<Pattern>();
    }

    public abstract class CharPattern {}

    public abstract class SingleChar : CharPattern {
        public abstract char Value { get; }
    }

    public class AsciiChar : SingleChar {
        [CharRange("0z")] char value;
        public override char Value => value;
    }

    public class HexChar : SingleChar {
        [Literal("\\x")] Unnamed _;
        [CharRange("09", "AF", "af", Min = 2, Max = 2)] string hexValue;
        public override char Value => (char)byte.Parse(hexValue);
    }

    public class ControlChar : SingleChar {
        [Literal("\\x")] Unnamed _;
        [CharRange("AZ")] char id;
        public override char Value => (char)(id - 'A' + 1);
    }

    public class UnicodeChar : SingleChar {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af", Min = 4, Max = 4)] string hexValue;
        public override char Value => (char)ushort.Parse(hexValue);
    }

    public class OctalChar : SingleChar {
        [Literal("\\")] Unnamed _;
        [CharRange("07", Min = 3, Max = 3)] string octalValue;
    }

    public class NumericRef : Pattern {
        [Literal("\\")] Unnamed _;
        [CharRange("19")] char number;
    }

    public class ExtendedUnicodeChar : SingleChar
    {
        [Literal("\\u")] Unnamed _;
        [CharRange("09", "AF", "af", Min = 0, Max = Int32.MaxValue)] string hexValue;
        public override char Value => (char)uint.Parse(hexValue);
    }

    public class Range : CharPattern
    {
        [Term] SingleChar start;
        [Literal("-")] Unnamed _;
        [Term] SingleChar end;
    }

    public class CharClass : CharPattern
    {
        [Literal("\\")] Unnamed _;
        [CharSet("wWdDsSbB")] char id;
    }

    public class SimpleEscape : SingleChar
    {
        [Literal("\\")] Unnamed _;
        [CharSet("tnvfr0+*?^$\\.[]{}()|/")] char c;
    }

    public class Repeat : Pattern
    {
        [Term] Expression inner;
        [CharSet("*+?")] char repeater;
        [Optional, Literal("?")] string lazy;
    }

    public class Quantified : Pattern
    {
        [Term] Expression inner;
        [Literal("{")] Unnamed _;
        [Term] int min;
        [Literal(","), Optional] Unnamed __;
        [Term] int? max;
        [Literal("}")] Unnamed ___;
    }

    public class Set : Pattern
    {
        [Optional, Literal("^")] string invert;
        [SurroundBy("[", "]")] List<CharPattern> items;
    }

    public class Any : Pattern
    {
        [Literal(".")] Unnamed _;
    }
}