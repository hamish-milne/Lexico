#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System.Collections.Generic;
using Lexico;

namespace Json
{
    [WhitespaceSurrounded]
    public class JsonDocument
    {
        JsonValue value;
    }

    public abstract class JsonValue
    {
    }

    public class JsonNumber : JsonValue
    {
        float value;
        public float Value => value;
    }

    public class JsonString : JsonValue
    {
        [Literal("\"")] Unnamed _;
        [Regex("[^\"]*")] string value;
        [Literal("\"")] Unnamed __;
    }

    [WhitespaceSurrounded]
    struct JsonSeparator
    {
        [Literal(",")] Unnamed __;
    }

    [WhitespaceSeparated]
    public class JsonArray : JsonValue
    {
        [Literal("[")] Unnamed _;
        [SeparatedBy(typeof(JsonSeparator))] List<JsonValue> values;
        [Literal("]")] Unnamed __;
    }

    [WhitespaceSeparated]
    public class JsonProperty
    {
        JsonValue name;
        [Literal(":")] Unnamed _;
        JsonValue value;
    }

    [WhitespaceSeparated]
    public class JsonDictionary : JsonValue
    {
        [Literal("{")] Unnamed _;
        [SeparatedBy(typeof(JsonSeparator))] List<JsonProperty> properties;
        [Literal("}")] Unnamed __;
    }
}