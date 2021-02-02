using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Lexico
{
    // public class Label {

    // }

    // public abstract class Var {
    // }

    // public abstract class GlobalVar {

    // }

    // public enum CompareOp {
    //     Equal,
    //     NotEqual,
    //     Less,
    //     Greater,
    //     LessOrEqual,
    //     GreaterOrEqual
    // }

    // enum BinaryOp {
    //     Add,
    //     Subtract
    // }

    public enum ResultMode
    {
        None,
        Mutate,
        Modify,
        Output
    }

    public class Context
    {
        public Context(Emitter emitter, ResultMode resultMode, Label? success, Label failure, string? name, IEnumerable<Feature> features)
        {
            Result = resultMode;
            Failure = failure;
            Success = success;
            Emitter = emitter;
            Name = name;
            Features = features;
        }

        public Var Length => this.GetFeature<String>().Length;
        public Var Position => this.GetFeature<String>().Position;
        public Var Sequence => this.GetFeature<String>().Sequence;
        public ResultMode Result { get; }
        public Label Failure { get; }
        public Label? Success { get; }
        public Emitter Emitter { get; }
        public string? Name { get; }
        public IEnumerable<Feature> Features { get; }
    }

    public interface Feature
    {
        Context Before(IParser parser, Context context, ref bool skipContent);
        void After(IParser parser, Context original, Context modified);
    }

    // public interface Emitter
    // {
    //     Type TypeOf(Var stackSlot);
    //     Label Label();
    //     Var Const(object value, Type type);
    //     Var Var(object? initial, Type type);
    //     void Copy(Var dst, Var src);
    //     GlobalVar Global(object? initial, Type type);
    //     Var GlobalRef(GlobalVar global);
    //     Var Default(Type type);
    //     Var Create(Type type);
    //     void Set(Var variable, object value);
    //     void Increment(Var variable, int amount);
    //     void Jump(Label label);
    //     void Compare(Var lhs, CompareOp op, Var rhs, Label label);
    //     void CheckType(Var lhs, Type type, Label label);
    //     void CheckFlag(Var var, int flag, bool compare, Label label);
    //     void SetFlag(Var var, int flag, bool value);
    //     Var Difference(Var lhs, Var rhs);
    //     Var Call(Var? obj, MethodBase method, params Var[] arguments);
    //     void Mark(Label label);
    //     Var Index(Var sequence, Var index);
    //     Var Load(Var obj, MemberInfo member);
    //     void Store(Var obj, MemberInfo member, Var value);
    //     IDisposable Frame();
    //     Context MakeRecursive(Type outputType);
    //     void CallRecursive(Emitter callee, Var? output, Label? onSuccess, Label onFailure);
    // }



    static class ContextExtensions
    {
        public static (Var[] state, Label label) Save(this Context context)
        {
            var e = context.Emitter;
            var checkIlr = context.GetFeature<CheckILR>();
            return (new []{context.Position, checkIlr.Pos, checkIlr.Flags}
                .Select(x => context.Emitter.Copy(x))
                .ToArray(), context.Emitter.Label());
        }

        public static void Restore(this Context context, (Var[] state, Label label) savePoint)
        {
            var e = context.Emitter;
            var checkIlr = context.GetFeature<CheckILR>();
            context.Emitter.Mark(savePoint.Item2);
            context.Emitter.Copy(context.Position, savePoint.state[0]);
            context.Emitter.Copy(e.GlobalRef(checkIlr.Pos), savePoint.state[1]);
            context.Emitter.Copy(e.GlobalRef(checkIlr.Flags), savePoint.state[2]);
        }

        public static void Peek(this Context context, int offset)
        {
            var e = context.Emitter;
            e.Load(context.Position);
            if (offset != 0) {
                e.Const(offset);
                e.Operate(BOP.Subtract);
            }
            e.Operate(UOP.Index);
        }

        public static void RequireSymbols(this Context context, int count)
        {
            var e = context.Emitter;
            context.GetSymbolsRemaining();
            e.Const(count);
            e.Jump(CMP.Less, context.Failure);
        }

        public static void GetSymbolsRemaining(this Context context) {
            var e = context.Emitter;
            e.Load(context.Length);
            e.Load(context.Position);
            e.Operate(BOP.Subtract);
        }

        public static void Advance(this Context context, int count)
        {
            var e = context.Emitter;
            e.Load(context.Position);
            e.Const(count);
            e.Operate(BOP.Add);
            e.Store(context.Position);
        }

        public static void PopCachedResult(this Context context)
        {
            switch (context.Result) {
                case ResultMode.Modify:
                case ResultMode.Mutate:
                    context.Emitter.Pop();
                    break;
            }
        }

        public static bool HasResult(this Context context) {
            return context.Result switch {
                ResultMode.None => false,
                _ => true
            };
        }

        // public static void Compare(this Emitter emitter, Var variable, CompareOp op, object value, Label label) {
        //     emitter.Compare(variable, op, emitter.Const(value), label);
        // }

        public static void Child(this Context context, IParser parser, string? name, ResultMode result, Label? success, Label failure) {
            // using var _ = context.Emitter.Frame();
            new Context(
                context.Emitter,
                result,
                success,
                failure,
                name,
                context.Features).CompileWithFeatures(parser);
        }

        public static void CompileWithFeatures(this Context context, IParser parser)
        {
            var list = new List<(Feature feature, Context original, Context modified)>();
            var it = context;
            bool skipContent = false;
            foreach (var f in context.Features) {
                var original = it;
                var modified = f.Before(parser, it, ref skipContent);
                list.Insert(0, (f, original, modified));
                it = modified;
            }
            if (!skipContent) {
                parser.Compile(it);
            }
            foreach (var (f, original, modified) in list) {
                f.After(parser, original, modified);
            }
        }

        // public static Var Call(this Emitter emitter, Var obj, string method, params Var[] arguments)
        // {
        //     var methodInfo = emitter.TypeOf(obj).GetMethod(method, arguments.Select(emitter.TypeOf).ToArray()) ?? throw new ArgumentException("Method not found");
        //     return emitter.Call(obj, methodInfo, arguments);
        // }
        
        public static T GetFeature<T>(this Context context) where T : Feature {
            return context.Features.OfType<T>().First();
        }

        // public static Var Const(this Emitter emitter, object value) => emitter.Const(value, value.GetType());

        // public static Var Create(this Emitter emitter, Type type, params Var[] arguments)
        // {
        //     if (type.IsValueType && arguments.Length == 0) {
        //         return emitter.Default(type);
        //     }
        //     var ctor = type.GetConstructor(arguments.Select(emitter.TypeOf).ToArray()) ?? throw new ArgumentException("Constructor not found");
        //     return emitter.Call(null, ctor, arguments);
        // }

        // public static Var Copy(this Emitter emitter, Var variable)
        // {
        //     var v = emitter.Var(null, emitter.TypeOf(variable));
        //     emitter.Copy(v, variable);
        //     return v;
        // }

        // public static void Succeed(this Context context, Var? result) {
        //     // TODO: should void vars be allowed?
        //     if (context.Result != null && result != null && context.Emitter.TypeOf(context.Result) != typeof(void)) {
        //         context.Emitter.Copy(context.Result, result);
        //     }
        //     context.Succeed();
        // }

        
        public static void Succeed(this Context context) {
            if (context.Success != null) {
                context.Emitter.Jump(context.Success);
            }
        }

        public static void Fail(this Context context) {
            context.Emitter.Jump(context.Failure);
        }
    }
}