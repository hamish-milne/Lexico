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

    public static class ContextExtensions2
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
            => ArrayIndex(context.String, Add(context.Position, Constant(index)));
    }

    internal delegate bool Parser<T>(string input, ref T position, ref T value, ITrace trace);

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
        private readonly Dictionary<(IParser, bool), Expression> recursionTargets;
        private readonly List<IParser> recursionList;
        private readonly CompileContext topLevel;
        private readonly IParser source;
        private readonly CompileContext? parent;
        private readonly Expression ilrStack;

        private bool InTree(IParser parser) => source == parser || parent?.InTree(parser) == true;

        public void Append(Expression statement)
        {
            statements.Add(statement ?? throw new ArgumentNullException(nameof(statement)));
        }

        public Expression Cache(Expression value)
        {
            var v = Variable(value.Type);
            statements.Add(Assign(v, value));
            return v;
        }

        private CompileContext(Expression text, IParser source, Expression position, Expression result, LabelTarget onSuccess, LabelTarget onFail)
        {
            this.Success = onSuccess;
            this.Failure = onFail;
            this.Position = position;
            this.Result = result;
            this.Length = PropertyOrField(text, nameof(string.Length));
            this.String = text;
            this.recursionTargets = new Dictionary<(IParser, bool), Expression>();
            this.recursionList = new List<IParser>();
            this.topLevel = this;
            this.source = source;
            this.parent = null;
            this.ilrStack = Cache(Constant(default(ulong)));
            this.ClearILR = AndAssign(ilrStack, Constant(~(1 << recursionList.IndexOf(source))));
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
            this.topLevel = parent.topLevel;
            this.source = source;
            this.parent = parent;
            this.ilrStack = parent.ilrStack;
            this.ClearILR = AndAssign(ilrStack, Constant(~(1 << recursionList.IndexOf(source))));
        }

        public static Delegate Compile(IParser parser)
        {
            var position = Parameter(typeof(int));
            var text = Parameter(typeof(string));
            var onSuccess = Label();
            var onFail = Label();
            var result = Parameter(parser.OutputType);
            var context = new CompileContext(text, null!, position, result, onSuccess, onFail);
            context.Child(parser, result, onSuccess, onFail);
            var end = Label(typeof(bool));
            context.statements.AddRange(new Expression[]{
                Label(onSuccess),
                Return(end, Constant(true)),
                Label(onFail),
                Return(end, Constant(false)),
                Label(end)
            });
            return Lambda(typeof(Parser<>).MakeGenericType(parser.OutputType), context.MakeBlock(), text, position, result, Parameter(typeof(ITrace))).Compile();
        }

        public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            var outType = child.OutputType;
            if (outType == typeof(void)) {
                outType = typeof(Unnamed);
            }
            var recursionKey = (child, result != null);
            if (recursionTargets.TryGetValue(recursionKey, out var recurse))
            {
                // We are currently recursing, or did in the past, so we should use the placeholder value
                var tmp = Cache(Default(child.OutputType));
                statements.Add(IfThen(Not(Invoke(recurse, tmp)), Goto(onFail)));
                this.Succeed(tmp);
            }
            else if (CheckRecursion(child))
            {
                // Make a placeholder variable
                var placeholder = topLevel.Cache(Default(typeof(LocalParser<>).MakeGenericType(outType)));
                recursionTargets.Add(recursionKey, placeholder);
                recursionList.Add(child);
                if (recursionList.Count > 64) {
                    throw new NotSupportedException("Too many recursive parsers; max of 64");
                }

                // Since we're recursing, we need to compile a lambda for this parser
                var tmpResult = Parameter(outType);
                var tmpSuccess = Label();
                var tmpFail = Label();
                var childContext = new CompileContext(this, child, tmpResult, tmpSuccess, tmpFail);

                // Disallow immediate left recursion
                var currentBit = (ulong)1 << recursionList.IndexOf(child);
                childContext.Append(IfThen(NotEqual(Constant(0), And(ilrStack, Constant(currentBit))), Goto(tmpFail)));
                childContext.Append(OrAssign(ilrStack, Constant(currentBit)));
                child.Compile(childContext);

                // Convert the gotos to a boolean return value
                var end = Label(typeof(bool));
                childContext.statements.AddRange(new Expression[]{
                    Label(tmpSuccess),
                    Return(end, Constant(true)),
                    Label(tmpFail),
                    Return(end, Constant(false)),
                    Label(end)
                });

                // Set the placeholder to the lambda we just built up
                topLevel.Append(Assign(placeholder, Lambda(childContext.MakeBlock(), tmpResult)));

                // Now we can call the result for the top level
                var tmp = Cache(Default(child.OutputType));
                statements.Add(IfThen(Not(Invoke(placeholder, tmp)), Goto(onFail)));
                this.Succeed(tmp);
            }
            else if (InTree(child))
            {
                throw new InvalidOperationException($"{child} was recursed into inconsistently. Parsers must be stateless and repeatable.");
            }
            else
            {
                // No recursion; just dump the results in the current set
                var childContext = new CompileContext(this, child, result, onSuccess, onFail);
                child.Compile(childContext);
                Append(childContext.MakeBlock());
            }
        }

        private Expression MakeBlock() => Block(variables, statements);

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
            public void Append(Expression statement) { }
            public Expression Cache(Expression value) => Parameter(value.Type);
            public void Restore(LabelTarget savePoint) {}
            public LabelTarget Save() => Label();

            public HashSet<IParser> Children { get; } = new HashSet<IParser>();

            public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
            {
                Children.Add(child);
            }
        }

        private readonly Dictionary<IParser, bool> recursiveParsersCache;

        private bool CheckRecursion(IParser child)
        {
            if (!recursiveParsersCache.TryGetValue(child, out var result))
            {
                var ctx = new RecursionCheck();
                child.Compile(ctx);
                result = ctx.Children.Contains(child);
                recursiveParsersCache.Add(child, result);
            }
            return result;
        }


    }
}