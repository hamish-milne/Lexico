using System.Linq;
using System.Collections.Specialized;
using System.Collections.Generic;
using System;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// A stateless object that consumes text and turns it into values
    /// </summary>
    public interface IParser
    {
        Type OutputType { get; }
        void Compile(ICompileContext context);
    }

    public interface ICompileContext
    {
        LabelTarget Save();
        void Restore(LabelTarget savePoint);
        LabelTarget? Success { get; }
        LabelTarget Failure { get; }
        Expression Position { get; }
        Expression? Result { get; }
        Expression Length { get; }
        Expression Cache(Expression value);
        Expression String { get; }
        Expression ClearILR { get; }
        void Append(Expression statement);
        void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail);
    }

    public static class ContextExtensions
    {
        public static void Succeed(this ICompileContext context, Expression value)
        {
            if (context.Result != null) {
                context.Append(Assign(context.Result, value));
            }
            context.Succeed();
        }
        public static void Succeed(this ICompileContext context)
        {
            if (context.Success != null) {
                context.Append(Goto(context.Success));
            }
        }
        public static void Fail(this ICompileContext context)
            => context.Append(Goto(context.Failure));

        public static void Advance(this ICompileContext context, int length)
        {
            if (length == 0) {
                return;
            }
            context.Append(AddAssign(context.Position, Constant(length)));
            context.Append(context.ClearILR);
        }

        public static Expression Peek(this ICompileContext context, int index)
            => MakeIndex(context.String, typeof(string).GetProperty("Chars"),
                new []{index == 0 ? context.Position : Add(context.Position, Constant(index))});
    }

    internal delegate bool Parser<T>(string input, ref int position, ref T value, ITrace trace);

    internal class CompileContext : ICompileContext
    {
        public LabelTarget? Success { get; }
        public LabelTarget Failure { get; }
        public Expression Position { get; }
        public Expression? Result { get; }
        public Expression Length { get; }
        public Expression String { get; }
        public Expression ClearILR { get; }

        private delegate bool LocalParser<T>(ref T value);

        private readonly List<Expression> statements = new List<Expression>();
        private readonly List<ParameterExpression> variables = new List<ParameterExpression>();
        private readonly Dictionary<IParser, Expression> recursionTargets;
        private readonly List<IParser> recursionList;
        private readonly HashSet<IParser> knownNotRecursive;
        private readonly CompileContext topLevel;
        private readonly IParser? source;
        private readonly CompileContext? parent;
        private readonly Expression ilrStack;
        private readonly Expression? byRefPosition;
        private readonly Expression? byRefResult;
        private readonly Expression? trace;

        private bool InTree(IParser parser) => source == parser || parent?.InTree(parser) == true;

        public void Append(Expression statement)
        {
            statements.Add(statement ?? throw new ArgumentNullException(nameof(statement)));
        }

        public Expression Cache(Expression value)
        {
            var v = Variable(value.Type);
            statements.Add(Assign(v, value));
            variables.Add(v);
            return v;
        }

        private CompileContext(Expression text, Expression position, Expression result, LabelTarget onSuccess, LabelTarget onFail, Expression? trace)
        {
            this.Success = onSuccess;
            this.Failure = onFail;
            byRefPosition = position;
            byRefResult = result;
            this.Position = Cache(position);
            this.Result = Cache(result);
            this.Length = PropertyOrField(text, nameof(string.Length));
            this.String = text;
            this.recursionTargets = new Dictionary<IParser, Expression>();
            this.recursionList = new List<IParser>();
            this.knownNotRecursive = new HashSet<IParser>();
            this.topLevel = this;
            this.source = null;
            this.parent = null;
            this.ilrStack = Cache(Constant(default(ulong)));
            this.trace = trace;
            this.ClearILR = Empty();
        }

        private CompileContext(CompileContext parent, IParser source, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            this.Success = onSuccess;
            this.Failure = onFail;
            this.Position = parent.Position;
            this.Result = result;
            this.Length = parent.Length;
            this.String = parent.String;
            this.recursionTargets = parent.recursionTargets;
            this.recursionList = parent.recursionList;
            this.knownNotRecursive = parent.knownNotRecursive;
            this.topLevel = parent.topLevel;
            this.source = source;
            this.parent = parent;
            this.ilrStack = parent.ilrStack;
            this.trace = parent.trace;
            this.ClearILR = AndAssign(ilrStack, Constant(~((ulong)1 << recursionList.IndexOf(source))));
        }

        public static Delegate Compile(IParser parser, bool trace)
        {
            var position = Parameter(typeof(int).MakeByRefType());
            var text = Parameter(typeof(string));
            var onSuccess = Label();
            var onFail = Label();
            var result = Parameter(parser.OutputType.MakeByRefType());
            var traceParam = Parameter(typeof(ITrace));
            var context = new CompileContext(text, position, result, onSuccess, onFail, trace ? traceParam : null);
            context.Child(parser, context.Result, onSuccess, onFail);
            var block = context.MakeFunctionBlock();
            return Lambda(
                typeof(Parser<>).MakeGenericType(parser.OutputType),
                block,
                text, position, result, traceParam
            ).Compile();
        }

        public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            var outType = child.OutputType;
            if (outType == typeof(void)) {
                outType = typeof(Unnamed);
            }

            void CallRecursionTarget(Expression recurse)
            {
                var tmp = Cache(Default(outType));
                statements.Add(IfThen(Not(Invoke(recurse, tmp)), Goto(onFail)));
                if (result != null && child.OutputType != typeof(void)) {
                    Append(Assign(result, tmp));
                }
                if (onSuccess != null) {
                    Append(Goto(onSuccess));
                }
            }

            if (recursionTargets.TryGetValue(child, out var recurse))
            {
                // We are currently recursing, or did in the past, so we should use the placeholder value
                CallRecursionTarget(recurse);
            }
            else if (CheckRecursion(child))
            {
                // Make a placeholder variable
                var placeholder = topLevel.Cache(Default(typeof(LocalParser<>).MakeGenericType(outType)));
                recursionTargets.Add(child, placeholder);
                recursionList.Add(child);
                if (recursionList.Count > 64) {
                    throw new NotSupportedException("Too many recursive parsers; max of 64");
                }

                // Since we're recursing, we need to compile a lambda for this parser
                var tmpResult = Parameter(outType.MakeByRefType());
                var tmpSuccess = Label();
                var tmpFail = Label();
                var childContext = new CompileContext(this, child, tmpResult, tmpSuccess, tmpFail);

                // Disallow immediate left recursion
                // TODO: Don't do ILR check if ILR isn't possible
                var currentBit = (ulong)1 << recursionList.IndexOf(child);
                // TODO: Add ILR check back in (for concrete exprs?)
                //childContext.Append(IfThen(NotEqual(Constant((ulong)0), And(ilrStack, Constant(currentBit))), Goto(tmpFail)));
                childContext.Append(OrAssign(ilrStack, Constant(currentBit)));
                child.Compile(childContext);

                // Set the placeholder to the lambda we just built up
                topLevel.Append(Assign(placeholder, Lambda(placeholder.Type, childContext.MakeFunctionBlock(), tmpResult)));

                // Now we can call the result for the top level
                CallRecursionTarget(placeholder);
            }
            else if (InTree(child))
            {
                throw new InvalidOperationException($"{child} was recursed into inconsistently. Parsers must be stateless and repeatable.");
            }
            else
            {
                // No recursion; just dump the results in the current set
                if (trace != null) {
                    Append(Call(trace, nameof(ITrace.Push), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(null, typeof(string))));
                    var childSuccess = Label();
                    var childFail = Label();
                    var childContext = new CompileContext(this, child, result, childSuccess, childFail);
                    child.Compile(childContext);
                    Append(Block(childContext.variables, childContext.statements));
                    statements.AddRange(new Expression[]{
                        Label(childFail),
                        Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(false), Constant(null), Constant(new StringSegment())),
                        Goto(onFail),
                        Label(childSuccess),
                        Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(true), result == null ? (Expression)Constant(null) : Convert(result, typeof(object)), Constant(new StringSegment())),
                        onSuccess == null ? (Expression)Empty() : Goto(onSuccess)
                    });
                } else {
                    var childContext = new CompileContext(this, child, result, onSuccess, onFail);
                    child.Compile(childContext);
                    Append(Block(childContext.variables, childContext.statements));
                }
            }
        }

        private Expression MakeFunctionBlock()
        {
            // TODO: Put trace stuff into general 'make block' function
            // Convert the gotos to a boolean return value
            var end = Label(typeof(bool));
            var saveRefArgs = byRefResult == null ? (Expression)Empty()
                : Block(Assign(byRefResult, Result), Assign(byRefPosition, Position));
            var traceSuccess = trace == null ? (Expression)Empty()
                : Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(source, typeof(IParser)), Constant(true), Result == null ? (Expression)Constant(null) : Convert(Result, typeof(object)), Constant(new StringSegment()));
            var traceFail = trace == null ? (Expression)Empty()
                : Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(source, typeof(IParser)), Constant(false), Constant(null), Constant(new StringSegment()));
            var traceStart = trace == null ? (Expression)Empty()
                : Call(trace, nameof(ITrace.Push), Type.EmptyTypes, Constant(source, typeof(IParser)), Constant(null, typeof(string)));
            return Block(variables, new []{traceStart}.Concat(statements).Concat(new Expression[]{
                Label(Success ?? throw new InvalidOperationException()),
                traceSuccess,
                saveRefArgs,
                Return(end, Constant(true)),
                Label(Failure),
                traceFail,
                saveRefArgs,
                Return(end, Constant(false)),
                Label(end, Constant(false))
            }));
        }

        private readonly Dictionary<LabelTarget, (Expression pos, Expression stack)> savePoints
            = new Dictionary<LabelTarget, (Expression, Expression)>();

        public void Restore(LabelTarget savePoint)
        {
            var (pos, stack) = savePoints[savePoint];
            Append(Label(savePoint));
            Append(Assign(Position, pos));
            Append(Assign(ilrStack, stack));
        }

        public LabelTarget Save()
        {
            var restore = Label();
            savePoints.Add(restore, (Cache(Position), Cache(ilrStack)));
            return restore;
        }

        private class RecursionCheck : ICompileContext
        {
            public LabelTarget? Success => null;
            public LabelTarget Failure { get; } = Label();
            public Expression Position { get; } = Parameter(typeof(int));
            public Expression? Result => null;
            public Expression Length { get; } = Parameter(typeof(int));
            public Expression String { get; } = Parameter(typeof(string));
            public Expression ClearILR { get; } = Empty();
            public void Append(Expression statement) {}
            public Expression Cache(Expression value) => Parameter(value.Type);
            public void Restore(LabelTarget savePoint) {}
            public LabelTarget Save() => Label();

            public HashSet<IParser> Children { get; } = new HashSet<IParser>();
            public CompileContext parent;

            public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
            {
                if (!parent.recursionTargets.ContainsKey(child) && Children.Add(child)) {
                    child.Compile(this);
                }
            }
        }

        private bool CheckRecursion(IParser child)
        {
            if (knownNotRecursive.Contains(child)) {
                return false;
            }
            var ctx = new RecursionCheck{parent = this};
            child.Compile(ctx);
            var result = ctx.Children.Contains(child);
            if (!result) {
                knownNotRecursive.Add(child);
            }
            return result;
        }
    }
}