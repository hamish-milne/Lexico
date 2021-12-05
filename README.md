![Nuget](https://img.shields.io/nuget/v/Lexico?logo=nuget)

# Getting started

## Installation

Head over to the [NuGet listing](https://www.nuget.org/packages/Lexico/) and install the package version you want in your project.

## Introduction

Lexico is a magical parsing library for .NET. Why magical? Because it's so easy to write parsers with Lexico, it feels like magic!

Have you worked on a problem that involves text manipulation, and groaned at the thought of importing and learning a huge parser dependency, writing a load of EBNF or hundreds of calls to a grammar API, only to get back an opaque and error-prone parse tree object? Or going the hand-written route and iterating manually over a string, hoping that it's guaranteed valid because you \*really\* don't want to think about what happens if it's not? Then maybe Lexico is the library you're looking for!

Lexico is being developed from the ground up to make text comprehension a breeze, whether you're a compiler author, or just need a hand decoding a configuration string. Lexico aims to compete not just with full on parser generators like ANTLR and Eto.Parse, but also built-in features like regular expressions.

Here are a a few reasons to use Lexico:

* **Simple and powerful** - Optionals, alternatives, left recursion, and infix precedence work out of the box, without the typical pitfalls and complexity of other libraries
* **Strong and safe** - The output of any Parse call is an object of the type you requested, ready to be used right away
* **Code integration** - Save even more time and effort by integrating functionality \(like name validation, evaluation, or post-processing\) directly into the grammar objects
* **Awesome speed** - No per-parse overhead and minimal allocations make Lexico blazing fast, even compared to hand-built alternatives! \(perf stats to come!\)
* **Infinite extension** - Make custom parsers and integrate them with your grammar by implementing just two methods.
* **NO** separate grammar language
* **NO** complex API calls to build the grammar
* **NO** mandatory code generation step
* **NO** intermediary parse tree objects
* **NO** dependencies.. at all! \(only .NET Standard 2.0\)

## My first grammar

In Lexico, grammars are defined using the type system you know and love, and are decoded into instances of those same types!

```csharp
struct MyFirstGrammar {
    [Term] public int numberOne;
    [Term] Whitespace _;
    [Term] public float numberTwo;
}

var result = Lexico.Parse<MyFirstGrammar>("123   45.6");
```

Let's go through line-by-line:

* A non-abstract class, or struct, is known as a Sequence. Its members are parsed one after another, in the order they are declared in.
* The `[Term]` attribute tells Lexico to include a member as part of the Sequence.
* The `int` and `float` members will match integers and floating-point numbers, respectively.
* The `Whitespace` type matches any amount of whitespace and discards the result \(because it's just punctuation\).
* Finally we call `Parse` to match the grammar, and we get a result of that type!

{% hint style="info" %}
`Parse` will throw on failure, but you can use `TryParse` to get a boolean success value
{% endhint %}

There's a lot more to Lexico, but this gives you an idea of how easy text comprehension can be.

## Terminals

A Terminal is simply a grammar element that isn't made up of any other grammars. The simplest Terminal is a Literal, a string that must be matched exactly. Other terminals include Number and Regex.

{% hint style="info" %}
Under some definitions, "terminal" refers to the smallest possible building block of a grammar, like a single character or contiguous range. In Lexico, Terminals can match any number of characters, can have internal structure, and even be context dependent - the only distinction is the lack of reliance on other grammars.
{% endhint %}

```csharp
struct KeyValue {
    [Regex(@"\w+")] string key;
    [Literal(":")] Unnamed _;
    [Term] int value;
}
```

* First, a `Regex` terminal matches any number of \(but at least one\) 'word' character, and saves the result in the `key` field. Because we're already defining a grammar attribute, we don't need an additional `[Term]` attribute.
* Then a `Literal` terminal matches a single colon. We don't care about saving the result because it's just punctuation, so we can tell Lexico to match then ignore it by setting the field type to `Unnamed` 
* Finally, we match an integer. In this case, since we rely on the default Number parser, we need an explicit `[Term]` attribute to include it in the sequence

## Options and alternatives

Often, your input text will contain one of a number of different constructs. Operations on a calculator, for example: you can have addition, subtraction, individual numbers, and more. In Lexico, these are called Alternatives. We define Alternatives with an abstract class or interface, the 'base type', and then implementing that base type as many times as we like.

```csharp
abstract class Expression {
    public abstract float Value { get; }
}

class Add : Expression {
    [Term] float a;
    [Literal("+")] Unnamed _;
    [Term] float b;

    public override float Value => a + b;
}

class Cosine : Expression {
    [Literal("cos(")] Unnamed _;
    [Term] float a;
    [Literal(")"] Unnamed __;

    public override float Value => (float)Math.Cos(a);
}

var result = Lexico.Parse<Expression>("2+2").Value;
```

* Here, our base type is `Expression`, with an abstract `Value` property. I wonder what that could be for..?
* Our first alternative is `Add`, which comprises two numbers with a literal "+" between them. We can override the `Value` property and put some functionality in there, allowing our program code to live side-by-side with the grammar definition
* Our second alternative is `Cosine`, comprising "cos\(", a number, then "\)". Like `Add`, we override `Value` and put a different function there.
* Finally we can call `Parse` with our top-level grammar, the base type. Since `Expression` is an Alternative, Lexico will return an instance of the first grammar that matches, which in this case is `Add`, so `result` is 4.

