#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Lexico.Test
{
    public class ComplexTests
    {
        private readonly ITestOutputHelper _outputHelper;
        public ComplexTests(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

        [Fact]
        public void CalculatorTest()
        {
            var expr = Lexico.Parse<Calculator.Expression>("5-(3/2)^(2+1)");
            Assert.Equal(1.625f, expr.Value);
        }

        [Fact]
        public void JsonTest()
        {
            var expr = Lexico.Parse<Json.JsonDocument>(@"
            {
                ""foo"": [1, 2, 3.0],
                5: ""bar"",
                [6.1]: {""baz"": ""bat""}
            }
            ");
            Assert.NotNull(expr);
        }

        private class Word
        {
            [Regex(@"[\w]+")] public string Value { get; set; }
        }

        private class Dash
        {
            [Literal("-")] private Unnamed _;
        }

        private class Semicolon
        {
            [Literal(";")] private Unnamed _;
        }

        private abstract class Item
        {
            [IndirectLiteral(nameof(LeadingTrivia))] protected Unnamed _;
            [Term, WhitespaceSurrounded] public Word Word;
            [Optional, SeparatedBy(typeof(Dash))] public List<int> Numbers;
            [Optional, SeparatedBy(typeof(Dash))] public List<Word> Words;
            [IndirectAlternative(nameof(TrailingParser))] public object Trailing;

            protected abstract string LeadingTrivia { get; }
            protected abstract Type[] TrailingParser { get; }
        }

        private class TestItemA : Item
        {
            protected override string LeadingTrivia { get; } = "A";
            protected override Type[] TrailingParser { get; } = {typeof(Semicolon)};
        }

        private class TestItemB : Item
        {
            protected override string LeadingTrivia { get; } = "B";
            protected override Type[] TrailingParser { get; } = {typeof(Semicolon), typeof(Dash)};
        }

        [
            Theory,
            InlineData("A first 5-3-1;", "first", new[]{5, 3, 1}),
            InlineData("A second 2-6-100-34;", "second", new[]{2, 6, 100, 34}),
            InlineData("B third 432-234-543-53425-", "third", new[]{432, 234, 543, 53425}),
            InlineData("B fourth 2;", "fourth", new[]{2}),
            InlineData("B fifth some-random-lol-words;", "fifth", null, "some", "random", "lol", "words"),
        ]
        public void CombinedTest(string text, string word, int[] numbers, params string[] words)
        {
            Assert.True(Lexico.TryParse(text, out Item result, new XunitTrace(_outputHelper)), "Parsing failed");
            Assert.Equal(word, result.Word.Value);
            Assert.Equal(numbers, result.Numbers);
            if (words.Length == 0) {
                words = null;
            }
            Assert.Equal(words, result.Words?.Select(w => w.Value).ToArray());
        }
    }
}
