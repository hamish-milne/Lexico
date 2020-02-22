#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Lexico;

namespace Json
{
    [WhitespaceSurrounded, MultiLine, TopLevel]
    public class JsonDocument
    {
        [Term] JsonValue value;
    }

    public abstract class JsonValue
    {
    }

    public class JsonNumber : JsonValue
    {
        [Term] float value;

        public override string ToString() => value.ToString();
    }

    public class JsonString : JsonValue
    {
        [Literal("\"")] Unnamed _;
        [Regex(@"(?:(?:\\.)|(\\u[a-fA-F0-9]{4})|[^""\\])*")] string value;
        [Literal("\"")] Unnamed __;

        private static string Escapes(Match match)
        {
            return match.Value[1] switch {
                'b' => "\b",
                'f' => "\f",
                'n' => "\n",
                'r' => "\r",
                't' => "\t",
                '\\' => "\\",
                '/' => "/",
                '"' => "\"",
                'u' => ((char)ushort.Parse(match.Value.AsSpan().Slice(1), NumberStyles.AllowHexSpecifier)).ToString(),
                var x => throw new FormatException($"Invalid escape sequence: `{x}`")
            };
        }

        public override string ToString() => Regex.Replace(value, @"\\(?:(?:u[a-fA-F0-9]{4})|.)", Escapes);
    }

    [WhitespaceSurrounded, MultiLine]
    struct JsonSeparator
    {
        [Literal(",")] Unnamed __;
    }

    [WhitespaceSeparated, MultiLine]
    public class JsonArray : JsonValue
    {
        [Literal("[")] Unnamed _;
        [SeparatedBy(typeof(JsonSeparator)), Optional] List<JsonValue> values;
        [Literal("]")] Unnamed __;
    }

    [WhitespaceSeparated, MultiLine]
    public class JsonProperty
    {
        [Term] JsonValue name;
        [Literal(":")] Unnamed _;
        [Term] JsonValue value;
    }

    [WhitespaceSeparated, MultiLine]
    public class JsonDictionary : JsonValue
    {
        [Literal("{")] Unnamed _;
        [SeparatedBy(typeof(JsonSeparator)), Optional] List<JsonProperty> properties;
        [Literal("}")] Unnamed __;
    }

    public class JsonTrue : JsonValue
    {
        [Literal("true")] Unnamed _;

        public override string ToString() => "true";
    }

    public class JsonFalse : JsonValue
    {
        [Literal("false")] Unnamed _;

        public override string ToString() => "false";
    }

    public class JsonNull : JsonValue
    {
        [Literal("null")] Unnamed _;

        public override string ToString() => "null";
    }
}