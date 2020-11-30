using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using bsn.GoldParser.Grammar;
using bsn.GoldParser.Parser;
using Eto.Parse;
using Eto.Parse.Samples.Json;
using Irony.Samples.Json;
using Lexico.Json;
using Newtonsoft.Json.Linq;

namespace Lexico.Benchmarks
{
    [MemoryDiagnoser]
    public class JsonBenchmark
    {
        public class Program
        {
            public static void Main(string[] args)
            {
                var summary = BenchmarkRunner.Run<JsonBenchmark>();
            }
        }

        [ParamsSource(nameof(TestData))]
        public JsonTestData JsonString;

        public static IEnumerable<JsonTestData> TestData { get; } = new List<JsonTestData>
        {
            new JsonTestData("sample-small.json"),
            new JsonTestData("sample-large.json"),
        };
        
        public class JsonTestData
        {
            public string Json { get; }
            private readonly string _name;

            public JsonTestData(string name)
            {
                Json = File.ReadAllText(CombinedPath(name));
                _name = name;
            }

            public static implicit operator string(JsonTestData d) => d.Json;

            public override string ToString() => _name;
        }

        private static string CombinedPath(string fileName) => Path.Combine("Json", fileName);
        private readonly CompiledGrammar _jsonGrammar = CompiledGrammar.Load(new BinaryReader(new FileStream(CombinedPath("JSON.egt"), FileMode.Open)));
        private readonly Grammar _etoJsonGrammar = new JsonGrammar();

        [Benchmark(Baseline = true)]
        public void LexicoJson()
        {
            var result = Lexico.Parse<JsonDocument>(JsonString);
            if (result.value == null) throw new Exception("Parsing failed");
        }
        
        [Benchmark]
        public void LexicoJsonWithNoOpTrace()
        {
            var result = Lexico.Parse<JsonDocument>(JsonString, new NoTrace());
            if (result.value == null) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void NewtonsoftJsonConvert()
        {
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject(JsonString);
            if (result == null) throw new Exception("Parsing failed");
        }

        // TODO: UTF8Json
        // TODO: JIL

        [Benchmark]
        public void NewtonsoftLINQ()
        {
            var result = JObject.Parse(JsonString);
            if (!result.HasValues) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void EtoParseDirect()
        {
            var result = _etoJsonGrammar.Match(JsonString);
            if (!result.Success) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void EtoParseHelpers()
        {
            var result = JsonObject.Parse(JsonString);
            if (result.Count < 1) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void SpracheJson()
        {
            var result = SpracheJSON.JSON.Parse(JsonString);
            if (!result.Pairs.Any()) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void SuperpowerJson()
        {
            if (!Superpower.JsonParser.JsonParser.TryParse(JsonString, out var value, out var error, out var errorPosition)) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void ServiceStackJson()
        {
            var result = ServiceStack.Text.JsonObject.Parse(JsonString);
            // Need to force ServiceStack to descend into arrays
            var child = result.ArrayObjects("result");
            for (var i = 0; i < child.Count; i++)
            {
                var item = child[i];
                item.ArrayObjects("tags");
                item.ArrayObjects("friends");
            }
        }

        [Benchmark]
        public void IronyJson()
        {
            var result = new Irony.Parsing.Parser(new IronyJsonGrammar()).Parse(JsonString);
            if(result.HasErrors()) throw new Exception("Parsing failed");
        }

        [Benchmark]
        public void bsnGold()
        {
            using var reader = new StringReader(JsonString);
            var tokenizer = new Tokenizer(reader, _jsonGrammar);
            var processor = new LalrProcessor(tokenizer, true);
            var parseMessage = processor.ParseAll();
            if (parseMessage != ParseMessage.Accept) throw new Exception("Parsing failed");
        }
    }
}