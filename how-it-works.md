---
description: Internals and extension points
---

# How it works

## Parsers

The most fundamental type used by Lexico's internals is `IParser`, a single-method interface that looks like this:

```csharp
public interface IParser
{
    bool Matches(ref IContext context, ref object? value);
}
```

Put simply, a Parser will, given some text, tell us whether that text matches, how many characters is consumed, and what the decoded value is. Success is determined by the boolean return value, but getting text and advancing the cursor is done using the `context` object. We won't go into the full details here, but the Context is responsible for managing the input buffer, advancing the cursor as we parse the text, plus dealing with other 'global' concerns like caching, logging, and recursion. Context objects are externally immutable; as we advance the cursor, a different object is returned, which is why the argument is passed by reference.

The `value` argument is used to return the decoded object, such as a string, number, or class instance. In some cases there may be a suggested or pooled value already, which will be passed as an initial value, but the parser can ignore this and return any value it chooses.

Parser instances must be externally immutable and stateless, as the `Matches` method is assumed to be thread-safe.

## Parser generation

To generate Parsers, Lexico uses the same attribute instances used to annotate the types and members that define them. All such attributes implement the `IParserGenerator` interface, defined like so:

```csharp
public interface IParserGenerator
{
    int Priority { get; }
    IParser Create(MemberInfo member, ChildParser child, IConfig config);
    bool AddDefault(MemberInfo member);
}
```

Note that the only sensible MemberInfos passed to the generator are FieldInfo, PropertyInfo, and Type. The use of other member types \(EventInfo, AccessorInfo etc.\) probably doesn't make sense in the context of parsing, but remains available for future extension.

Typically there are multiple generator attributes on each member, located in the following ways:

* On the field/property, e.g. `[Literal("foo")] Unnamed _;`
* On the Type, e.g. `[WhitespaceSeparated] struct MyType {...`
* Applied by default to certain members \(for instance, 'Number' is applied to int, float, etc.\)

The result is a 'chain' of attributes, ordered by Priority such that higher priority attributes are evaluated first. The highest priority generator, typically a modifier like Optional or SeparatedBy, will invoke the `child` function passed to it to evaluate the next attribute in the chain, and so on.

For example, a definition like `[SeparatedBy(","), WhitespaceSurrounded] MyStruct? _;` might have the following generator chain: `[WhitespaceSurrounded] -> [SeparatedBy(",")] -> [Optional] -> [Sequence]`

This happens because the Optional generator's `AddDefault` method returns True for all Types that are specialisations of `Nullable<T>` , and the Sequence generator does the same for all non-abstract Types, but Optional has a higher Priority than Sequence.

If a generator invokes `child` , but there is no child generator to call, we call this an "invalid parser chain" \(though we couch this in simpler language for the user error messages\). An easy way to get this error is to do something like `[Optional] string _;` - the Optional generator expects a child, but `string` has no default parser to give. The usual fix would be to add a Terminal attribute, which is guaranteed not to require any children.

The opposite condition can also occur, when the head of the chain returns a parser without invoking `child` enough to consume the entire chain. This can only really happen if multiple Terminal attributes are defined on one member \(though the AttributeUsage will usually prevent this at compile time\). This is very likely caused by an user error in the grammar definition, which we call an "ambiguous parser chain".

