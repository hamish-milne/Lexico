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
        bool CheckRecursion(IParser child);
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
        void Append(Expression statement);
        void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail);
    }

    public static class ContextExtensions2
    {
        public static void Succeed(this ICompileContext context, Expression value)
        {
            if (context.Result != null) {
                context.Append(Expression.Assign(context.Result, value));
            }
            context.Succeed();
        }
        public static void Succeed(this ICompileContext context)
        {
            if (context.Success != null) {
                context.Append(Expression.Goto(context.Success));
            }
        }
        public static void Fail(this ICompileContext context)
            => context.Append(Expression.Goto(context.Failure));
        
        public static void Advance(this ICompileContext context, int length)
            => context.Append(Expression.AddAssign(context.Position, Expression.Constant(length)));
        
        public static Expression Peek(this ICompileContext context, int index)
            => Expression.ArrayIndex(context.String, Expression.Add(context.Position, Expression.Constant(index)));
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

        private delegate bool LocalParser<T>(ref T value);

        private readonly List<Expression> statements = new List<Expression>();
        private readonly List<ParameterExpression> variables = new List<ParameterExpression>();
        private readonly Dictionary<(IParser, bool), Expression> recursionTargets;
        private readonly HashSet<(IParser, bool)> finishedRecursionTargets;
        private readonly CompileContext topLevel;
        private readonly IParser source;
        private readonly CompileContext? parent;

        //private readonly Expression dlr;

        private bool InTree(IParser parser) => source == parser || parent?.InTree(parser) == true;

        public void Append(Expression statement)
        {
            statements.Add(statement ?? throw new ArgumentNullException(nameof(statement)));
        }

        public Expression Cache(Expression value)
        {
            var v = Expression.Variable(value.Type);
            statements.Add(Expression.Assign(v, value));
            return v;
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
            this.finishedRecursionTargets = parent.finishedRecursionTargets;
            this.topLevel = parent.topLevel;
            this.source = source;
            this.parent = parent;
        }

        public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            var recursionKey = (child, result != null);
            if (InTree(child))
            {
                // Parsing 'child' within itself - we are doing some sort of recursion
                // Add or get a placeholder for the parse function...
                if (!recursionTargets.TryGetValue(recursionKey, out var recurse)) {
                    var outType = child.OutputType;
                    if (outType == typeof(void)) {
                        outType = typeof(Unnamed);
                    }
                    recurse = topLevel.Cache(Default(typeof(LocalParser<>).MakeGenericType(outType)));
                    recursionTargets.Add(recursionKey, recurse);
                }
                // And then call it. The value will be filled in by the earliest call to compile 'child'
                var tmp = Cache(Expression.Default(child.OutputType));
                statements.Add(IfThen(Not(Invoke(recurse, tmp)), Goto(onFail)));
                this.Succeed(tmp);
            }
            else if (recursionTargets.TryGetValue(recursionKey, out var recurse))
            {
                var tmp = Cache(Expression.Default(child.OutputType));
                statements.Add(IfThen(Not(Invoke(recurse, tmp)), Goto(onFail)));
                this.Succeed(tmp);
            }
            else if (child.CheckRecursion(child))
            {
                // Since we're recursing, we need to compile a lambda for this parser
                var tmpResult = result == null ? null : Cache(result);
                var childContext = new CompileContext(this, child, result, onSuccess, onFail);
                child.Compile(childContext);
                if (recursionTargets.TryGetValue(recursionKey, out var placeholder)
                    && !finishedRecursionTargets.Contains(recursionKey))
                {
                    // Recursion happened, but was filled in with placeholders for now. We need to make the final value.
                    topLevel.Append(Assign(placeholder, Lambda(childContext.MakeBlock(), (ParameterExpression)result!)));
                }
                // Either the recursion already existed or we just finished it; either way we can call the result
                var tmp = Cache(Expression.Default(child.OutputType));
                statements.Add(IfThen(Not(Invoke(placeholder, tmp)), Goto(onFail)));
                this.Succeed(tmp);
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

        public void Restore(LabelTarget savePoint)
        {
            throw new NotImplementedException();
        }

        public LabelTarget Save()
        {
            throw new NotImplementedException();
        }


    }
}