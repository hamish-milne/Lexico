#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System.Collections.Generic;
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
        public float Value => value;
    }

    public class JsonString : JsonValue
    {
        [Literal("\"")] Unnamed _;
        [Regex("[^\"]*")] string value;
        [Literal("\"")] Unnamed __;
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
        [SeparatedBy(typeof(JsonSeparator))] List<JsonValue> values;
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
        [SeparatedBy(typeof(JsonSeparator))] List<JsonProperty> properties;
        [Literal("}")] Unnamed __;
    }
}