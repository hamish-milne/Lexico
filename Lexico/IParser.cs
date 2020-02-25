using System.Linq;
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
        void Append(Expression statement);
        void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail);
        void Recursive(IParser child);
    }

    public static class ContextExtensions
    {
        public static void Succeed(this ICompileContext context, Expression value)
        {
            if (context.Result != null) {
                context.Append(Assign(context.Result, Convert(value, context.Result.Type)));
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
        }

        public static Expression Peek(this ICompileContext context, int index)
            => MakeIndex(context.String, typeof(string).GetProperty("Chars"),
                new []{index == 0 ? context.Position : Add(context.Position, Constant(index))});
    }

    [Flags]
    public enum CompileFlags
    {
        None = 0,
        Trace = 1 << 0,
        CheckImmediateLeftRecursion = 1 << 1,
        Memoizing = 1 << 2,
        AggressiveMemoizing = 1 << 3,
        ValueMemoizing = 1 << 4,
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
        private readonly Expression ilrPos;
        private readonly Expression? byRefPosition;
        private readonly Expression? byRefResult;
        private readonly Expression? trace;
        private readonly bool enableIlrCheck;
        private Expression? memo;
        private readonly List<IParser> parsersById;
        private bool doIlrChecks;

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

        private struct Memo
        {
            private HashSet<(int, int, ulong)> cache;

            public void Init() {
                cache = new HashSet<(int, int, ulong)>();
            }

            public bool Check(int id, int pos, ulong ilr) => cache.Contains((id, pos, ilr));

            public bool Remember(int id, int pos, ulong ilr) => cache.Add((id, pos, ilr));
        }

        private struct AggressiveMemo
        {
            private HashSet<(int, int)> cache;

            public void Init() {
                cache = new HashSet<(int, int)>();
            }

            public bool Check(int id, int pos) => cache.Contains((id, pos));

            public bool Remember(int id, int pos) => cache.Add((id, pos));
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
            this.ilrPos = Cache(position);
            this.trace = trace;
            this.parsersById = new List<IParser>();
        }

        private CompileContext(CompileContext parent, IParser source, Expression? result, LabelTarget? onSuccess, LabelTarget onFail, bool enableIlrCheck)
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
            this.ilrPos = parent.ilrPos;
            this.trace = parent.trace;
            this.enableIlrCheck = enableIlrCheck;
            this.memo = parent.memo;
            this.parsersById = parent.parsersById;
            this.doIlrChecks = parent.doIlrChecks;
        }

        public static Delegate Compile(IParser parser, CompileFlags flags)
        {
            var position = Parameter(typeof(int).MakeByRefType());
            var text = Parameter(typeof(string));
            var onSuccess = Label();
            var onFail = Label();
            var result = Parameter(parser.OutputType.MakeByRefType());
            var traceParam = Parameter(typeof(ITrace));
            var context = new CompileContext(text, position, result, onSuccess, onFail,
                (flags & CompileFlags.Trace) != 0 ? traceParam : null);
            if ((flags & (CompileFlags.Memoizing | CompileFlags.AggressiveMemoizing)) != 0) {
                var memoType = (flags & CompileFlags.AggressiveMemoizing) != 0 ? typeof(AggressiveMemo) : typeof(Memo);
                context.memo = context.Cache(New(memoType));
                context.Append(Call(context.memo, nameof(Memo.Init), Type.EmptyTypes));
            }
            if ((flags & CompileFlags.CheckImmediateLeftRecursion) != 0) {
                context.doIlrChecks = true;
            }
            context.Child(parser, context.Result, onSuccess, onFail);
            var block = context.MakeFunctionBlock();
            return Lambda(
                typeof(Parser<>).MakeGenericType(parser.OutputType),
                block,
                text, position, result, traceParam
            ).Compile();
        }

        public void Recursive(IParser child)
        {
            if (!recursionTargets.ContainsKey(child))
            {
                var outType = GetOutputType(child);

                // Make a placeholder variable
                var placeholder = topLevel.Cache(Default(typeof(LocalParser<>).MakeGenericType(outType)));
                recursionTargets.Add(child, placeholder);

                // Since we're recursing, we need to compile a lambda for this parser
                var tmpResult = Parameter(outType.MakeByRefType());
                var tmpSuccess = Label();
                var tmpFail = Label();
                var childContext = new CompileContext(this, child, tmpResult, tmpSuccess, tmpFail, doIlrChecks);
                child.Compile(childContext);

                // Set the placeholder to the lambda we just built up
                topLevel.Append(Assign(placeholder, Lambda(placeholder.Type, childContext.MakeFunctionBlock(), tmpResult)));
            }

            // Now we can call the result for the top level
            CallRecursionTarget(child, Result, Success, Failure);
        }

        Type GetOutputType(IParser child) => child.OutputType == typeof(void) ? typeof(Unnamed) : child.OutputType;

        void CallRecursionTarget(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            var recurse = recursionTargets[child];
            var tmp = Cache(Default(GetOutputType(child)));
            statements.Add(IfThen(Not(Invoke(recurse, tmp)), Goto(onFail)));
            if (result != null && child.OutputType != typeof(void)) {
                Append(Assign(result, tmp));
            }
            if (onSuccess != null) {
                Append(Goto(onSuccess));
            }
        }

        public void Child(IParser child, Expression? result, LabelTarget? onSuccess, LabelTarget onFail)
        {
            if (enableIlrCheck)
            {
                if (!recursionList.Contains(child)) {
                    recursionList.Add(child);
                    if (recursionList.Count > 64) {
                        throw new NotSupportedException("Too many recursive sources; max of 64");
                    }
                }
                // Disallow immediate left recursion.
                // If the parent is a recursion target (e.g. 'Expression')..
                // AND the child has already been recursed into..
                // AND the position is the same..
                // THEN it's immediate left recursion, so fail.
                // OTHERWISE, if the position *has* changed then clear the ILR flags and update it.
                var currentBit = (ulong)1 << recursionList.IndexOf(child);
                Append(IfThen(And(Equal(ilrPos, Position), NotEqual(Constant((ulong)0), And(ilrStack, Constant(currentBit)))), Goto(onFail)));
                Append(IfThen(NotEqual(ilrPos, Position), Block(Assign(ilrPos, Position), Assign(ilrStack, Constant((ulong)0)))));
                Append(OrAssign(ilrStack, Constant(currentBit)));
            }

            var memoOnFail = onFail;
            Expression remember = null!;
            if (memo != null) {
                if (!parsersById.Contains(child)) {
                    parsersById.Add(child);
                }
                var parserId = Constant(parsersById.IndexOf(child));
                if (memo.Type == typeof(AggressiveMemo)) {
                    Append(IfThen(Call(memo, nameof(AggressiveMemo.Check), Type.EmptyTypes, parserId, Position), Goto(onFail)));
                    remember = Call(memo, nameof(AggressiveMemo.Remember), Type.EmptyTypes, parserId, Cache(Position));
                } else {
                    Append(IfThen(Call(memo, nameof(Memo.Check), Type.EmptyTypes, parserId, Position, ilrStack), Goto(onFail)));
                    remember = Call(memo, nameof(Memo.Remember), Type.EmptyTypes, parserId, Cache(Position), Cache(ilrStack));
                }
                memoOnFail = Label();
            }

            if (recursionTargets.ContainsKey(child))
            {
                // We are currently recursing, or did in the past, so we should use the placeholder value
                CallRecursionTarget(child, result, onSuccess, memoOnFail);
            }
            else
            {
                // No recursion; just dump the results in the current set
                if (trace != null && !(child is UnaryParser)) {
                    Append(Call(trace, nameof(ITrace.Push), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(null, typeof(string))));
                    var childSuccess = Label();
                    var childFail = Label();
                    var childContext = new CompileContext(this, child, result, childSuccess, childFail, false);
                    child.Compile(childContext);
                    // If recursion became apparent during compilation...
                    if (recursionTargets.ContainsKey(child)) {
                        CallRecursionTarget(child, result, onSuccess, memoOnFail);
                    }
                    else {
                        Append(Block(childContext.variables, childContext.statements));
                        statements.AddRange(new Expression[]{
                            Label(childFail),
                            Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(false), Constant(null), Constant(new StringSegment())),
                            Goto(memoOnFail),
                            Label(childSuccess),
                            Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(true), result == null ? (Expression)Constant(null) : Convert(result, typeof(object)), Constant(new StringSegment())),
                            onSuccess == null ? (Expression)Empty() : Goto(onSuccess)
                        });
                    }
                } else {
                    var childContext = new CompileContext(this, child, result, onSuccess, memoOnFail, false);
                    child.Compile(childContext);
                    // If recursion became apparent during compilation...
                    if (recursionTargets.ContainsKey(child)) {
                        CallRecursionTarget(child, result, onSuccess, memoOnFail);
                    } else {
                        Append(Block(childContext.variables, childContext.statements));
                    }
                }
            }

            if (memo != null) {
                var skip = Label();
                Append(Goto(skip));
                Append(Label(memoOnFail));
                Append(remember!);
                Append(Goto(onFail));
                Append(Label(skip));
            }
        }

        private Expression MakeFunctionBlock()
        {
            // TODO: Put trace stuff into general 'make block' function
            // TODO: Add names and text to trace calls
            // Convert the gotos to a boolean return value
            var doTrace = trace != null && source != null && !(source is UnaryParser);
            var end = Label(typeof(bool));
            var saveRefArgs = byRefResult == null ? (Expression)Empty()
                : Block(Assign(byRefResult, Result), Assign(byRefPosition, Position));
            var traceSuccess = !doTrace ? (Expression)Empty()
                : Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(source, typeof(IParser)), Constant(true), Result == null ? (Expression)Constant(null) : Convert(Result, typeof(object)), Constant(new StringSegment()));
            var traceFail = !doTrace ? (Expression)Empty()
                : Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, Constant(source, typeof(IParser)), Constant(false), Constant(null), Constant(new StringSegment()));
            var traceStart = !doTrace ? (Expression)Empty()
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

        private readonly Dictionary<LabelTarget, (Expression pos, Expression stack, Expression stackPos)> savePoints
            = new Dictionary<LabelTarget, (Expression, Expression, Expression)>();

        public void Restore(LabelTarget savePoint)
        {
            var (pos, stack, stackPos) = savePoints[savePoint];
            Append(Label(savePoint));
            Append(Assign(Position, pos));
            Append(Assign(ilrStack, stack));
            Append(Assign(ilrPos, stackPos));
        }

        public LabelTarget Save()
        {
            var restore = Label();
            savePoints.Add(restore, (Cache(Position), Cache(ilrStack), Cache(ilrPos)));
            return restore;
        }
    }
}