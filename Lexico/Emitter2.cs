using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Lexico
{

    public interface Var : IDisposable {
        Type Type { get; }
    }

    public interface Label {
    }

    public interface Subroutine {
    }

    public enum BOP {
        Add,
        Subtract,
        Or,
        And,
    }

    public enum UOP {
        Not,
        Negate,
        Index,
    }

    public enum CMP {
        Less,
        Greater,
        LessOrEqual,
        GreaterOrEqual,
        Equal,
        NotEqual,
        IsNull,
    }

    public interface Emitter
    {
        Var Local(Type type);
        Var Global(string name, Type type);
        void Const(object value);
        void Null();
        void Create(Type type);
        void Load(Var v);
        void Operate(BOP bop);
        void Operate(UOP uop);
        void SetSequence();
        void Compare(CMP cmp);
        Label Label();
        void Mark(Label label);
        void Jump(CMP cmp, Label label);
        void Jump(Label label);
        void Jump(bool value, Label label);
        void Call(MethodBase method);
        void Store(Var v);
        void Pop();
        void Dup();
        void StoreField(FieldInfo field);
        void LoadField(FieldInfo field);
        void Finish();
        Subroutine BeginSub();
        void EndSub();
        void CallSub(Subroutine program);
    }

    class Program2 : Emitter
    {
        private static bool IsInt(Type type) => conversions.ContainsKey(type);

        public void Call(MethodBase method)
        {
            var pArray = method.GetParameters();
            for (int i = 0; i < pArray.Length; i++) {
                if (IsInt(pArray[i].ParameterType)) {
                    code.Add((Int64)Opcode.BoxArg);
                    code.Add(pArray.Length - i);
                    code.Add(intTypes.IndexOf(pArray[i].ParameterType));
                }
            }
            while (argArrays.Count <= pArray.Length) {
                argArrays.Add(new object?[argArrays.Count]);
            }
            Type returnType;
            if (method is MethodInfo method1) {
                code.Add((Int64)(method1.IsStatic ? Opcode.CallStatic : Opcode.Call));
                code.Add(pArray.Length);
                code.Add(methods.GetOrAdd(method1));
                returnType = method1.ReturnType;
            } else if (method is ConstructorInfo constructor) {
                code.Add((Int64)Opcode.CallConstructor);
                code.Add(pArray.Length);
                code.Add(constructors.GetOrAdd(constructor));
                returnType = constructor.DeclaringType;
            } else {
                throw new ArgumentException();
            }
            if (IsInt(returnType)) {
                code.Add((Int64)Opcode.Unbox);
                code.Add(intTypes.IndexOf(returnType));
            }
        }

        public void Const(object value)
        {
            if (IsInt(value.GetType())) {
                code.Add((Int64)Opcode.PushInt);
                code.Add(conversions[value.GetType()].otoi(value));
            } else {
                code.Add((Int64)Opcode.PushConst);
                code.Add(consts.GetOrAdd(value));
            }
        }

        public void Create(Type type)
        {
            if (IsInt(type)) {
                code.Add((Int64)Opcode.PushInt);
                code.Add(0);
            } else {
                code.Add((Int64)Opcode.Create);
                code.Add(types.GetOrAdd(type));
            }
        }

        public void Dup()
        {
            code.Add((Int64)Opcode.Dup);
        }

        class GVar : Var {
            public GVar(string name, Type type) {
                Name = name;
                Type = type;
            }
            public string Name { get; }
            public Type Type { get; }

            public void Dispose() {}
        }
        class LVar : Var {
            private readonly Program2 parent;
            public LVar(Program2 parent, Type type) {
                this.parent = parent;
                Type = type;
            }
            public Type Type { get; }

            public void Dispose() => parent.Free(this);
        }

        public Var Global(string name, Type type)
        {
            var v = new GVar(name, type);
            globals.Add(v);
            return v;
        }

        public Var Local(Type type)
        {
            var v = new LVar(this, type);
            var slot = locals.IndexOf(null);
            if (slot < 0) {
                locals.Add(v);
            } else {
                locals[slot] = v;
            }
            return v;
        }

        private void Free(LVar var)
        {
            var idx = locals.IndexOf(var);
            if (idx < 0) {
                throw new ArgumentException("Local variable double-free");
            }
            locals[idx] = null;
        }

        public Label Label() => new MyLabel();

        public void Mark(Label label)
        {
            ((MyLabel)label).Mark(this);
        }

        public void Jump(Label label)
        {
            code.Add((Int64)Opcode.Jump);
            code.Add(((MyLabel)label).GetTarget(this));
        }

        public void Jump(bool value, Label label)
        {
            code.Add((Int64)(value ? Opcode.JumpTrue : Opcode.JumpFalse));
            code.Add(((MyLabel)label).GetTarget(this));
        }

        public void Jump(CMP cmp, Label label)
        {
            if (CompareInternal(cmp)) {
                code.Add((Int64)Opcode.JumpTrue);
            } else {
                code.Add((Int64)Opcode.JumpFalse);
            }
            code.Add(((MyLabel)label).GetTarget(this));
        }

        private bool CompareInternal(CMP cmp)
        {
            code.Add((Int64)(cmp switch {
                CMP.Equal => Opcode.Equal,
                CMP.NotEqual => Opcode.Equal,
                CMP.Less => Opcode.Less,
                CMP.GreaterOrEqual => Opcode.Less,
                CMP.Greater => Opcode.Greater,
                CMP.LessOrEqual => Opcode.Greater,
                CMP.IsNull => Opcode.IsNull,
                _ => throw new ArgumentOutOfRangeException()
            }));
            return cmp switch {
                CMP.NotEqual => false,
                CMP.GreaterOrEqual => false,
                CMP.LessOrEqual => false,
                _ => true
            };
        }

        public void Compare(CMP cmp)
        {
            if (!CompareInternal(cmp)) {
                Operate(UOP.Not);
            }
        }

        static int CheckIndex(int idx)
        {
            if (idx < 0) {
                throw new ArgumentException("Variable was de-allocated");
            }
            return idx;
        }

        public void Load(Var v)
        {
            if (v is LVar l) {
                code.Add((Int64)(IsInt(v.Type) ? Opcode.LoadIntLocal : Opcode.LoadObjLocal));
                code.Add(CheckIndex(locals.IndexOf(l)));
            } else if (v is GVar g) {
                code.Add((Int64)(IsInt(v.Type) ? Opcode.LoadIntGlobal : Opcode.LoadObjGlobal));
                code.Add(CheckIndex(globals.IndexOf(g)));
            } else {
                throw new ArgumentException();
            }
        }

        public void Store(Var v)
        {
            if (v is LVar l) {
                code.Add((Int64)(IsInt(v.Type) ? Opcode.StoreIntLocal : Opcode.StoreObjLocal));
                code.Add(CheckIndex(locals.IndexOf(l)));
            } else if (v is GVar g) {
                code.Add((Int64)(IsInt(v.Type) ? Opcode.StoreIntGlobal : Opcode.StoreObjGlobal));
                code.Add(CheckIndex(globals.IndexOf(g)));
            } else {
                throw new ArgumentException();
            }
        }

        public void StoreField(FieldInfo field)
        {
            if (IsInt(field.FieldType)) {
                code.Add((Int64)Opcode.Box);
                code.Add(intTypes.IndexOf(field.FieldType));
            }
            code.Add((Int64)Opcode.StoreField);
            code.Add(fields.GetOrAdd(field));
        }

        public void LoadField(FieldInfo field)
        {
            code.Add((Int64)Opcode.LoadField);
            code.Add(fields.GetOrAdd(field));
            if (IsInt(field.FieldType)) {
                code.Add((Int64)Opcode.Unbox);
                code.Add(intTypes.IndexOf(field.FieldType));
            }
        }

        public void Return()
        {
            code.Add((Int64)Opcode.Return);
        }

        public void Operate(BOP bop)
        {
            code.Add((Int64) (bop switch {
                BOP.Add => Opcode.Add,
                BOP.And => Opcode.And,
                BOP.Or => Opcode.Or,
                BOP.Subtract => Opcode.Subtract,
                _ => throw new ArgumentOutOfRangeException()
            }));
        }

        public void Operate(UOP uop)
        {
            code.Add((Int64) (uop switch {
                UOP.Not => Opcode.Not,
                UOP.Negate => Opcode.Negate,
                UOP.Index => Opcode.Index,
                _ => throw new ArgumentOutOfRangeException()
            }));
        }

        public void SetSequence()
        {
            code.Add((Int64)Opcode.SetSequence);
        }

        private readonly Stack<Label> subEnd = new Stack<Label>();

        class MySub : Subroutine {
            public int address;
        }

        public Subroutine BeginSub()
        {
            var endLabel = Label();
            subEnd.Push(endLabel);
            Jump(endLabel);
            return new MySub{address = code.Count};
        }

        public void EndSub()
        {
            Mark(subEnd.Pop());
        }

        public void CallSub(Subroutine subroutine)
        {
            code.Add((Int64)Opcode.Subroutine);
            code.Add(((MySub)subroutine).address);
        }

        public void Pop()
        {
            code.Add((Int64)Opcode.Pop);
        }

        public void Null()
        {
            code.Add((Int64)Opcode.Null);
        }

        public void Finish()
        {

        }


        enum Opcode : Int64 {
            INVALID = 1000000000,
            LoadObjLocal,
            LoadIntLocal,
            LoadObjGlobal,
            LoadIntGlobal,
            StoreObjLocal,
            StoreIntLocal,
            StoreObjGlobal,
            StoreIntGlobal,
            PushConst,
            PushInt,
            Pop,
            Dup,
            Null,
            Create,
            Less,
            Greater,
            Equal,
            IsNull,
            Jump,
            JumpTrue,
            JumpFalse,
            LoadField,
            StoreField,
            Box,
            BoxArg,
            Unbox,
            Call,
            CallStatic,
            CallConstructor,
            Add,
            Subtract,
            Or,
            And,
            Not,
            Negate,
            Subroutine,
            Return,
            SetSequence,
            Index,

        }

        struct ListOf<T> {
            public T[] List;
            public int Count;

            public void Add(T value) {
                Count++;
                if (List == null) {
                    List = new T[4];
                } if (Count >= List.Length) {
                    Array.Resize(ref List, List.Length * 2);
                }
                List[Count - 1] = value;
            }

            public int GetOrAdd(T value) {
                var idx = Array.IndexOf(List, value);
                if (idx < 0) {
                    idx = Count;
                    Add(value);
                }
                return idx;
            }
        }

        private ListOf<Int64> code;
        private ListOf<Type> types;
        private ListOf<FieldInfo> fields;
        private ListOf<MethodInfo> methods;
        private ListOf<object?[]> argArrays;
        private ListOf<object> consts;
        private ListOf<ConstructorInfo> constructors;
        private readonly List<GVar> globals = new List<GVar>();
        private readonly List<LVar?> locals = new List<LVar?>();
        

        private class MyLabel : Label {
            private List<int> jumpPoints = new List<int>();
            private int target = -1;

            public void Mark(Program2 parent) {
                if (target >= 0) {
                    throw new Exception("Label already marked");
                }
                target = parent.code.Count;
                foreach (var i in jumpPoints) {
                    parent.code.List[i] = target;
                }
                jumpPoints.Clear();
            }

            public int GetTarget(Program2 parent)
            {
                if (target < 0) {
                    jumpPoints.Add(parent.code.Count);
                }
                return target;
            }
        }

        private static readonly Dictionary<Type, (Func<object, long> otoi, Func<long, object> itoo)> conversions =
            new Dictionary<Type, (Func<object, long>, Func<long, object>)> {
                {typeof(int), (o => (int)o, i => (int)i)},
                {typeof(char), (o => (char)o, i => (char)i)},
                {typeof(long), (o => (long)o, i => (long)i)},
                {typeof(bool), (o => (bool)o ? -1 : 0, i => i != 0)},
            };
        
        static Program2()
        {
            intTypes = conversions.Keys.OrderBy(x => x.Name).ToArray();
            boxFns = intTypes.Select(x => conversions[x].itoo).ToArray();
            unboxFns = intTypes.Select(x => conversions[x].otoi).ToArray();
        }
        
        private static readonly IList<Type> intTypes;
        private static readonly Func<Int64, object>[] boxFns;
        private static readonly Func<object, Int64>[] unboxFns;

        public bool Execute(Func<string, object?> initGlobals, out object result)
        {
            var maxStack = 999;
            var iValues = new Int64[globals.Count + maxStack];
            var oValues = new object?[globals.Count + maxStack];
            for(int idx = 0; idx < globals.Count; idx++) {
                var g = globals[idx];
                var init = initGlobals(g.Name);
                if (init == null) {
                    continue;
                }
                if (IsInt(g.Type)) {
                    iValues[idx] = conversions[g.Type].otoi(init);
                } else {
                    oValues[idx] = init;
                }
            }
            string? str = null;
            long pc = 0;
            int sp = globals.Count;
            Execute(iValues, oValues, ref sp, ref pc, ref str);
            if (iValues[--sp] != 0) {
                result = oValues[--sp]!;
                return true;
            } else {
                result = null!;
                return false;
            }
        }

        void Execute(Int64[] iValues, object?[] oValues, ref int sp, ref Int64 pc, ref string? str)
        {
            var code = this.code.List;
            var consts = this.consts.List;
            var types = this.types.List;
            var fields = this.fields.List;
            var argArrays = this.argArrays.List;
            var methods = this.methods.List;
            var constructors = this.constructors.List;

            var iLocals = new Int64[locals.Count];
            var oLocals = new object?[locals.Count];
            while (true)
            switch ((Opcode)code[pc++]) {
            case Opcode.PushInt:
                iValues[sp++] = code[pc++];
                break;
            case Opcode.PushConst:
                oValues[sp++] = consts[code[pc++]];
                break;
            case Opcode.Pop:
                --sp;
                break;
            case Opcode.Dup:
                iValues[sp] = iValues[sp - 1];
                oValues[sp] = oValues[sp - 1];
                sp++;
                break;
            case Opcode.Less:
                --sp;
                iValues[sp - 1] = iValues[sp - 1] < iValues[sp] ? -1 : 0;
                break;
            case Opcode.Greater:
                --sp;
                iValues[sp - 1] = iValues[sp - 1] > iValues[sp] ? -1 : 0;
                break;
            case Opcode.Equal:
                --sp;
                iValues[sp - 1] = iValues[sp - 1] == iValues[sp] ? -1 : 0;
                break;
            case Opcode.IsNull:
                iValues[sp - 1] = oValues[sp - 1] == null ? -1 : 0;
                break;
            case Opcode.Jump:
                pc = code[pc];
                break;
            case Opcode.JumpFalse:
                if (iValues[--sp] != 0) {
                    pc++;
                } else {
                    pc = code[pc];
                }
                break;
            case Opcode.JumpTrue:
                if (iValues[--sp] != 0) {
                    pc = code[pc];
                } else {
                    pc++;
                }
                break;
            case Opcode.Create:
                oValues[sp++] = Activator.CreateInstance(types[pc++]);
                break;
            case Opcode.Box:
                oValues[sp - 1] = boxFns[code[pc++]](iValues[sp - 1]);
                break;
            case Opcode.BoxArg:
                var arg = code[pc++];
                oValues[sp - arg] = boxFns[code[pc++]](iValues[sp - arg]);
                break;
            case Opcode.Unbox:
                iValues[sp - 1] = unboxFns[code[pc++]](oValues[sp - 1]!);
                break;
            case Opcode.LoadField:
                oValues[sp - 1] = fields[code[pc++]].GetValue(oValues[sp - 1]);
                break;
            case Opcode.StoreField:
                fields[code[pc++]].SetValue(oValues[--sp], oValues[--sp]);
                break;
            case Opcode.Null:
                oValues[sp++] = null;
                break;
            case Opcode.LoadIntLocal:
                iValues[sp++] = iLocals[code[pc++]];
                break;
            case Opcode.LoadObjLocal:
                oValues[sp++] = oLocals[code[pc++]];
                break;
            case Opcode.StoreIntLocal:
                iLocals[code[pc++]] = iValues[--sp];
                break;
            case Opcode.StoreObjLocal:
                oLocals[code[pc++]] = oValues[--sp];
                break;
            case Opcode.LoadIntGlobal:
                iValues[sp++] = iValues[code[pc++]];
                break;
            case Opcode.LoadObjGlobal:
                oValues[sp++] = oValues[code[pc++]];
                break;
            case Opcode.StoreIntGlobal:
                iValues[code[pc++]] = iValues[--sp];
                break;
            case Opcode.StoreObjGlobal:
                oValues[code[pc++]] = oValues[--sp];
                break;
            case Opcode.Call: {
                var obj = oValues[--sp];
                var args = argArrays[code[pc++]];
                for (var i = 0; i < args.Length; i++) {
                    args[i] = oValues[--sp];
                }
                oValues[sp++] = methods[code[pc++]].Invoke(obj, args);
                break;
            }
            case Opcode.CallStatic: {
                var args = argArrays[code[pc++]];
                for (var i = 0; i < args.Length; i++) {
                    args[i] = oValues[--sp];
                }
                oValues[sp++] = methods[code[pc++]].Invoke(null, args);
                break;
            }
            case Opcode.CallConstructor: {
                var args = argArrays[code[pc++]];
                for (var i = 0; i < args.Length; i++) {
                    args[i] = oValues[--sp];
                }
                oValues[sp++] = constructors[code[pc++]].Invoke(args);
                break;
            }
            case Opcode.Add:
                --sp;
                iValues[sp] = iValues[sp] + iValues[sp + 1];
                break;
            case Opcode.Subtract:
                --sp;
                iValues[sp] = iValues[sp] - iValues[sp + 1];
                break;
            case Opcode.And:
                --sp;
                iValues[sp] = iValues[sp] & iValues[sp + 1];
                break;
            case Opcode.Or:
                --sp;
                iValues[sp] = iValues[sp] | iValues[sp + 1];
                break;
            case Opcode.Not:
                iValues[sp] = ~iValues[sp];
                break;
            case Opcode.Negate:
                iValues[sp] = -iValues[sp];
                break;
            case Opcode.Index:
                iValues[sp] = str![(int)iValues[sp]];
                break;
            case Opcode.Return:
                return;
            case Opcode.Subroutine:
                var prev = pc;
                pc = code[pc++];
                Execute(iValues, oValues, ref sp, ref pc, ref str);
                pc = prev + 1;
                break;
            case Opcode.SetSequence:
                str = (string?)oValues[--sp];
                break;
            }
        }
    }
}