using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Lexico
{
    public class Label {

    }

    public abstract class Var {
    }

    public abstract class GlobalVar {

    }

    public enum CompareOp {
        Equal,
        NotEqual,
        Less,
        Greater,
        LessOrEqual,
        GreaterOrEqual
    }

    enum BinaryOp {
        Add,
        Subtract
    }

    public class Context
    {
        public Context(Emitter emitter, Var? result, Label? success, Label failure, string? name, IEnumerable<Feature> features, bool canWriteResult)
        {
            Result = result;
            Failure = failure;
            Success = success;
            Emitter = emitter;
            Name = name;
            Features = features;
            CanWriteResult = canWriteResult;
        }

        public Var Length => Emitter.GlobalRef(this.GetFeature<String>().Length);
        public Var Position => Emitter.GlobalRef(this.GetFeature<String>().Position);
        public Var Sequence => Emitter.GlobalRef(this.GetFeature<String>().Sequence);
        public Var? Result { get; }
        public Label Failure { get; }
        public Label? Success { get; }
        public Emitter Emitter { get; }
        public string? Name { get; }
        public IEnumerable<Feature> Features { get; }
        public bool CanWriteResult { get; }
    }

    public interface Feature
    {
        Context Before(IParser parser, Context context);
        void After(IParser parser, Context original, Context modified);
    }

    public interface Emitter
    {
        Type TypeOf(Var stackSlot);
        Label Label();
        Var Const(object value, Type type);
        Var Var(object? initial, Type type);
        void Copy(Var dst, Var src);
        GlobalVar Global(object? initial, Type type);
        Var GlobalRef(GlobalVar global);
        Var Default(Type type);
        void Set(Var variable, object value);
        void Increment(Var variable, int amount);
        void Jump(Label label);
        void Compare(Var lhs, CompareOp op, Var rhs, Label label);
        void CheckFlag(Var var, int flag, bool compare, Label label);
        void SetFlag(Var var, int flag, bool value);
        Var Difference(Var lhs, Var rhs);
        Var Call(Var? obj, MethodBase method, params Var[] arguments);
        void Mark(Label label);
        Var Index(Var sequence, Var index);
        Var Load(Var obj, MemberInfo member);
        void Store(Var obj, MemberInfo member, Var value);
        IDisposable Frame();
        Context MakeRecursive(Type outputType);
        void CallRecursive(Emitter callee, Var? output, Label? onSuccess, Label onFailure);
    }



    static class ContextExtensions2
    {
        public static (Var state, Label label) Save(this Context context)
        {
            return (context.Emitter.Copy(context.Position), context.Emitter.Label());
        }

        public static void Restore(this Context context, (Var, Label) savePoint)
        {
            context.Emitter.Mark(savePoint.Item2);
            context.Emitter.Copy(savePoint.Item1, context.Position);
        }

        public static Var Peek(this Context context, int offset)
        {
            if (offset == 0) {
                return context.Emitter.Index(context.Sequence, context.Position);
            } else {
                var tmp = context.Emitter.Copy(context.Position);
                context.Emitter.Increment(tmp, offset);
                return context.Emitter.Index(context.Sequence, tmp);
            }
        }

        public static void RequireSymbols(this Context context, int count)
        {
            using var _ = context.Emitter.Frame();
            context.Emitter.Compare(
                context.GetSymbolsRemaining(),
                CompareOp.Less,
                count,
                context.Failure);
        }

        public static Var GetSymbolsRemaining(this Context context) {
            return context.Emitter.Difference(context.Length, context.Position);
        }

        public static void Advance(this Context context, int count)
        {
            context.Emitter.Increment(context.Position, count);
        }

        public static void Compare(this Emitter emitter, Var variable, CompareOp op, object value, Label label) {
            emitter.Compare(variable, op, emitter.Const(value), label);
        }

        public static void Child(this Context context, IParser parser, string? name, Var? result, Label? success, Label failure) {
            Child(context, parser, name, result, success, failure, result != null);
        }

        public static void Child(this Context context, IParser parser, string? name, Var? result, Label? success, Label failure, bool canWriteResult) {
            using var _ = context.Emitter.Frame();
            new Context(
                context.Emitter,
                result,
                success,
                failure,
                name,
                context.Features,
                canWriteResult).CompileWithFeatures(parser);
        }

        public static void CompileWithFeatures(this Context context, IParser parser)
        {
            var modifiedContext = context;
            foreach (var f in context.Features) {
                modifiedContext = f.Before(parser, context);
            }
            parser.Compile(modifiedContext);
            foreach (var f in context.Features) {
                f.After(parser, context, modifiedContext);
            }
        }

        public static Var Call(this Emitter emitter, Var obj, string method, params Var[] arguments)
        {
            var methodInfo = emitter.TypeOf(obj).GetMethod(method, arguments.Select(emitter.TypeOf).ToArray()) ?? throw new ArgumentException("Method not found");
            return emitter.Call(obj, methodInfo, arguments);
        }
        
        public static T GetFeature<T>(this Context context) where T : Feature {
            return context.Features.OfType<T>().First();
        }

        public static Var Const(this Emitter emitter, object value) => emitter.Const(value, value.GetType());

        public static Var Create(this Emitter emitter, Type type, params Var[] arguments)
        {
            var ctor = type.GetConstructor(arguments.Select(emitter.TypeOf).ToArray()) ?? throw new ArgumentException("Constructor not found");
            return emitter.Call(null, ctor, arguments);
        }

        public static Var Copy(this Emitter emitter, Var variable)
        {
            var v = emitter.Var(null, emitter.TypeOf(variable));
            emitter.Copy(v, variable);
            return v;
        }

        public static void Succeed(this Context context, Var? result) {
            if (context.Result != null && result != null) {
                context.Emitter.Copy(result, context.Result);
            }
            context.Succeed();
        }

        
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