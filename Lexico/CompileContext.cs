using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    internal class CompileContext : ICompileContext
    {
        public LabelTarget? Success { get; }
        public LabelTarget Failure { get; }
        public Expression Position { get; }
        public Expression? Result { get; }
        public Expression Length { get; }
        public Expression String { get; }
        public Expression UserObject { get; }
        public Expression? Cut { get; }

        private delegate bool LocalParser<T>(ref T value);

        private readonly List<Expression> statements = new List<Expression>();
        private readonly List<ParameterExpression> variables = new List<ParameterExpression>();
        private readonly List<ParameterExpression> variablePool = new List<ParameterExpression>();
        private readonly Dictionary<IParser, Expression> recursionTargets;
        private readonly List<IParser> recursionList;
        private readonly HashSet<IParser> knownNotRecursive;
        private readonly CompileContext topLevel;
        private readonly IParser? source;
        private readonly CompileContext? parent;
        private readonly Expression? ilrStack;
        private readonly Expression? ilrPos;
        private readonly Expression? byRefPosition;
        private readonly Expression? byRefResult;
        private readonly Expression? trace;
        private readonly bool enableIlrCheck;
        private Expression? memo;
        private readonly List<IParser> parsersById;
        private readonly bool doIlrChecks;

        private bool InTree(IParser parser) => source == parser || parent?.InTree(parser) == true;

        public void Append(Expression statement)
        {
            statements.Add(statement ?? throw new ArgumentNullException(nameof(statement)));
        }

        public Expression Cache(Expression value)
        {
            var v = variablePool
                .FirstOrDefault(x => x.Type == value.Type);
            if (v == null) {
                v = Variable(value.Type);
            } else {
                variablePool.Remove(v);
            }
            variables.Add(v);
            statements.Add(Assign(v, value));
            return v;
        }

        public void Release(Expression? variable)
        {
            if (variables.Remove(variable as ParameterExpression)) {
                variablePool.Add((ParameterExpression)variable);
            }
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

        private CompileContext(Expression text, Expression position, Expression result, LabelTarget onSuccess, LabelTarget onFail, Expression? trace, Expression userObject, bool doIlrChecks)
        {
            this.Success = onSuccess;
            this.Failure = onFail;
            this.byRefPosition = position;
            this.byRefResult = result;
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
            this.doIlrChecks = doIlrChecks;
            if (doIlrChecks) {
                this.ilrStack = Cache(Constant(default(ulong)));
                this.ilrPos = Cache(position);
            }
            this.trace = trace;
            this.UserObject = userObject;
            this.parsersById = new List<IParser>();
        }

        private CompileContext(CompileContext parent, IParser source, Expression? result, LabelTarget? onSuccess, LabelTarget onFail, bool enableIlrCheck, Expression? cut, bool keepVariables)
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
            this.UserObject = parent.UserObject;
            this.memo = parent.memo;
            this.parsersById = parent.parsersById;
            this.doIlrChecks = parent.doIlrChecks;
            this.Cut = cut;
            if (keepVariables) {
                this.variablePool = parent.variablePool;
            }
        }

        public static Delegate Compile(IParser parser, CompileFlags flags)
        {
            var attr = parser.OutputType.GetCustomAttribute<CompileFlagsAttribute>();
            if (attr != null) {
                flags |= attr.Value;
            }
            var position = Parameter(typeof(int).MakeByRefType());
            var text = Parameter(typeof(string));
            var onSuccess = Label();
            var onFail = Label();
            var result = Parameter(parser.OutputType.MakeByRefType());
            var traceParam = Parameter(typeof(ITrace));
            var userObject = Parameter(typeof(object));
            var context = new CompileContext(text, position, result, onSuccess, onFail,
                (flags & CompileFlags.Trace) != 0 ? traceParam : null, userObject,
                (flags & (CompileFlags.Memoizing | CompileFlags.CheckImmediateLeftRecursion)) != 0);
            if ((flags & (CompileFlags.Memoizing | CompileFlags.AggressiveMemoizing)) != 0) {
                var memoType = (flags & CompileFlags.AggressiveMemoizing) != 0 ? typeof(AggressiveMemo) : typeof(Memo);
                context.memo = context.Cache(New(memoType));
                context.Append(Call(context.memo, nameof(Memo.Init), Type.EmptyTypes));
            }
            context.Child(parser, null, context.Result, onSuccess, onFail, null);
            var block = context.MakeFunctionBlock();
            return Lambda(
                typeof(Parser<>).MakeGenericType(parser.OutputType),
                block,
                text, position, result, traceParam, userObject
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
                var childContext = new CompileContext(this, child, tmpResult, tmpSuccess, tmpFail, doIlrChecks, null, false);
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
            var r = Cache(Invoke(recurse, tmp));
            statements.Add(IfThen(Not(r), Goto(onFail)));
            if (result != null && child.OutputType != typeof(void)) {
                Append(Assign(result, tmp));
            }
            Release(tmp);
            Release(r);
            if (onSuccess != null) {
                Append(Goto(onSuccess));
            }
        }

        public void Child(IParser child, string? name, Expression? result, LabelTarget? onSuccess, LabelTarget onFail, Expression? cut)
        {
            if (recursionTargets.ContainsKey(child))
            {
                // We are currently recursing, or did in the past, so we should use the placeholder value
                // TODO: Add specific/named trace around this:
                CallRecursionTarget(child, result, onSuccess, onFail);
                return;
            }

            // TODO: Remove the 'is UnaryParser' check; instead don't trace on the recursion wrapper calls
            // TODO: Make sure that the Name info passes into UnaryParser
            var doTrace = trace != null && !(child is UnaryParser) && !child.Flags.HasFlag(ParserFlags.IgnoreInTrace);
            // In some cases we need to rewrite the result labels to add code at the end of parsing
            var childSuccess = doTrace ? Label() : onSuccess;
            var childFail = (doTrace || memo != null) ? Label() : onFail;

            var startPos = (memo != null || doTrace) ? Cache(Position) : null;
            var startIlr = ilrStack != null ? Cache(ilrStack) : null;

            var childContext = new CompileContext(this, child, result, childSuccess, childFail, false, cut, true);
            child.Compile(childContext);
            // If recursion became apparent during compilation...
            if (recursionTargets.ContainsKey(child)) {
                Release(startPos);
                Release(startIlr);
                CallRecursionTarget(child, result, onSuccess, onFail);
                return;
            }

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
                // TODO: Add ILR failures to trace?
                var currentBit = (ulong)1 << recursionList.IndexOf(child);
                Append(IfThen(And(Equal(ilrPos, Position), NotEqual(Constant((ulong)0), And(ilrStack, Constant(currentBit)))), Goto(onFail)));
                Append(IfThen(NotEqual(ilrPos, Position), Block(Assign(ilrPos, Position), Assign(ilrStack, Constant((ulong)0)))));
                Append(OrAssign(ilrStack, Constant(currentBit)));
            }



            // Trace begin
            Expression segmentExpr = null!;
            if (doTrace) {
                segmentExpr = New(stringSegment, String, startPos, Subtract(Position, startPos));
                Append(Call(trace, nameof(ITrace.Push), Type.EmptyTypes, Constant(child, typeof(IParser)), Constant(name, typeof(string))));
            }

            // Memo begin:
            // Don't try to re-parse sections we know will fail (if the Memoizing flag is enabled)
            var memoEnd = childFail;
            Expression remember = null!;
            if (memo != null) {
                memoEnd = Label();
                if (!parsersById.Contains(child)) {
                    parsersById.Add(child);
                }
                var parserId = Constant(parsersById.IndexOf(child));
                if (memo.Type == typeof(AggressiveMemo)) {
                    // Aggressize memo only cares about the parser ID and current position and *not* the ILR state.
                    // This means it may incorrectly report a parse failure if a potentially correct tree is blocked
                    // due to the ILR stack used on the first attempt. However it essentially guarantees linear complexity.
                    Append(IfThen(Call(memo, nameof(AggressiveMemo.Check), Type.EmptyTypes, parserId, Position), Goto(memoEnd)));
                    remember = Call(memo, nameof(AggressiveMemo.Remember), Type.EmptyTypes, parserId, startPos!);
                } else {
                    // 'Normal' memo cares about the position, parser ID, and ILR state, so it will never report
                    // a false negative, but may not prevent factorial complexity in all cases.
                    Append(IfThen(Call(memo, nameof(Memo.Check), Type.EmptyTypes, parserId, Position, ilrStack), Goto(memoEnd)));
                    remember = Call(memo, nameof(Memo.Remember), Type.EmptyTypes, parserId, startPos!, startIlr!);
                }
            }

            // The actual child parser code:
            // variables.AddRange(childContext.variables);
            if (childContext.variables.Count > 0) {
                statements.Add(Block(childContext.variables, childContext.statements));
            } else {
                statements.AddRange(childContext.statements);
            }
            // Append(Block(childContext.variables, childContext.statements));

            // Memo end
            if (memo != null) {
                var skip = Label();
                Append(Goto(skip));
                Append(Label(childFail));
                Append(remember!);
                if (!doTrace) {
                    Append(Label(memoEnd));
                    Append(Goto(onFail));
                }
                Append(Label(skip));
            }

            // Trace end
            if (doTrace) {
                var sourceExpr = Constant(child, typeof(IParser));
                statements.AddRange(new Expression[]{
                    Label(memoEnd),
                    Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, sourceExpr,
                        Constant(false),
                        Constant(null),
                        segmentExpr
                    ),
                    Goto(onFail),
                    Label(childSuccess),
                    Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, sourceExpr,
                        Constant(true),
                        result == null ? (Expression)Constant(null) : Convert(result, typeof(object)),
                        segmentExpr
                    ),
                    onSuccess == null ? (Expression)Empty() : Goto(onSuccess)
                });
            }

            Release(segmentExpr);
            Release(startPos);
            Release(startIlr);
            variables.AddRange(childContext.variables);
        }

        bool wasUsed = false;

        private static readonly ConstructorInfo stringSegment =
            typeof(StringSegment).GetConstructor(new []{typeof(string), typeof(int), typeof(int)});

        private BlockExpression MakeFunctionBlock()
        {
            if (wasUsed) {
                throw new Exception();
            }
            wasUsed = true;
            // TODO: Put trace stuff into general 'make block' function
            // Convert the gotos to a boolean return value
            var doTrace = trace != null && source != null && !(source is UnaryParser) && !source.Flags.HasFlag(ParserFlags.IgnoreInTrace);
            var end = Label(typeof(bool));
            var saveRefArgs = byRefResult == null ? (Expression)Empty()
                : Block(Assign(byRefResult, Result), Assign(byRefPosition, Position));
            if (doTrace)
            {
                var startPos = Variable(typeof(int), "startPos");
                var sourceExpr = Constant(source, typeof(IParser));
                var segmentExpr = New(stringSegment, String, startPos, Subtract(Position, startPos));
                return Block(variables.Concat(new []{startPos}).Concat(variablePool), new Expression[]{
                    Assign(startPos, Position),
                    Call(trace, nameof(ITrace.Push), Type.EmptyTypes, sourceExpr,
                        Constant(null, typeof(string))
                    ),
                }.Concat(statements).Concat(new Expression[]{
                    Label(Success ?? throw new InvalidOperationException()),
                    Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, sourceExpr,
                        Constant(true),
                        Result == null ? (Expression)Constant(null) : Convert(Result, typeof(object)),
                        segmentExpr
                    ),
                    saveRefArgs,
                    Return(end, Constant(true)),
                    Label(Failure),
                    Call(trace, nameof(ITrace.Pop), Type.EmptyTypes, sourceExpr,
                        Constant(false),
                        Constant(null),
                        segmentExpr
                    ),
                    saveRefArgs,
                    Return(end, Constant(false)),
                    Label(end, Constant(false))
                }));
            }
            return Block(variables.Concat(variablePool), statements.Concat(new Expression[]{
                Label(Success ?? throw new InvalidOperationException()),
                saveRefArgs,
                Return(end, Constant(true)),
                Label(Failure),
                saveRefArgs,
                Return(end, Constant(false)),
                Label(end, Constant(false))
            }));
        }

        private readonly Dictionary<LabelTarget, (Expression pos, Expression? stack, Expression? stackPos)> savePoints
            = new Dictionary<LabelTarget, (Expression, Expression?, Expression?)>();

        public void Restore(LabelTarget savePoint)
        {
            var (pos, stack, stackPos) = savePoints[savePoint];
            Append(Label(savePoint));
            Append(Assign(Position, pos));
            if (enableIlrCheck) {
                Append(Assign(ilrStack, stack));
                Append(Assign(ilrPos, stackPos));
            }
        }

        public LabelTarget Save()
        {
            var restore = Label();
            if (enableIlrCheck) {
                savePoints.Add(restore, (Cache(Position), Cache(ilrStack!), Cache(ilrPos!)));
            } else {
                savePoints.Add(restore, (Cache(Position), null, null));
            }
            return restore;
        }

        public void Release(LabelTarget target)
        {
            if (savePoints.TryGetValue(target, out var exprs)) {
                Release(exprs.pos);
                if (exprs.stack != null) {
                    Release(exprs.stack);
                }
                if (exprs.stackPos != null) {
                    Release(exprs.stackPos);
                }
                savePoints.Remove(target);
            }
        }
    }
}