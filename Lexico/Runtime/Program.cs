using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lexico
{
    class Runtime : Entry
    {
        private readonly Dictionary<IParser, Context> cache = new Dictionary<IParser, Context>();

        public bool TryParse(IParser parser, IEnumerable<Feature> features, string input, out object output)
        {
            if (!cache.TryGetValue(parser, out var ctx)) {
                ctx = Program.MakeRoot(parser.OutputType, features);
                ctx.CompileWithFeatures(parser);
                cache.Add(parser, ctx);
            }
            var pos = 0;
            return ((Program)ctx.Emitter).Execute(input, ref pos, out output);
        }
    }

    class Program : Emitter
    {
        class RuntimeVar : Var {
            public RuntimeVar(Type type, int index, bool isConst) {
                this.type = type;
                this.index = index;
                this.isConst = isConst;
            }
            public readonly int index;
            public readonly Type type;
            public readonly bool isConst;
        }

        class RuntimeLabel : Label {
            public int target = -1;
            public readonly List<int> jumpPoints = new List<int>();
        }

        enum OpCode {
            INVALID,
            Box,
            Unbox,
            Call,
            Construct,
            ObjEqual,
            IntEqual,
            IntCompare,
            JumpTrue,
            JumpFalse,
            Jump,
            IntSet,
            IntCopy,
            ObjCopy,
            LoadField,
            StoreField,
            AddImmediate,
            Subtract,
            Subroutine,
            Peek,
            Return
        }

        struct Operation {
            public OpCode opcode;
            public int lhs;
            public int rhs;
            public int result;
        }

        private RuntimeVar sequence;
        private RuntimeVar position;
        private RuntimeVar length;
        private RuntimeVar result;
        private int iStackSize = 1;
        private int oStackSize = 1;
        private readonly List<object?> constants = new List<object?>();
        private readonly List<Operation> code = new List<Operation>();
        private readonly List<(MethodBase method, int argCount)> methodTable = new List<(MethodBase, int)>();
        private readonly List<FieldInfo> fieldTable = new List<FieldInfo>();
        private readonly List<Type> typeTable = new List<Type>();
        private readonly List<Program> dependencies = new List<Program>();
        private readonly Stack<StackFrame> frames = new Stack<StackFrame>();
        private readonly List<RuntimeVar> pool = new List<RuntimeVar>();
        private readonly IEnumerable<Feature> features;

        public Program(Type outputType, IEnumerable<Feature> features) {
            Frame();
            position = Allocate(typeof(int));
            sequence = Allocate(typeof(string));
            length = Allocate(typeof(int));
            result = Allocate(outputType);
            this.features = features;
        }

        class StackFrame : IDisposable {
            private readonly Program parent;
            private readonly string dbg;
            private bool disposed;

            public StackFrame(Program parent)
            {
                this.parent = parent;
                this.dbg = new System.Diagnostics.StackTrace(2, true).ToString();
            }

            public readonly List<RuntimeVar> allocated = new List<RuntimeVar>();

            public void Dispose()
            {
                if (parent.frames.Pop() != this) {
                    throw new Exception("Frames must be disposd in order");
                }
                parent.pool.AddRange(allocated);
                allocated.Clear();
                this.disposed = true;
            }
        }

        private static readonly Dictionary<Type, (Func<object, int> otoi, Func<int, object> itoo)> conversions =
            new Dictionary<Type, (Func<object, int>, Func<int, object>)> {
                {typeof(int), (o => (int)o, i => i)},
                {typeof(bool), (o => (bool)o ? 1 : 0, i => i != 0)},
                {typeof(char), (o => (int)(char)o, i => (char)i)},
            };
        
        private static bool IsInt(RuntimeVar v) => conversions.ContainsKey(v.type);
        private static bool IsInt(Type t) => conversions.ContainsKey(t);

        public bool Execute(string sequence, ref int position, out object result)
        {
            var last = code.Last();
            var pc = 0;
            var test = false;
            var iValues = new int[iStackSize];
            var oValues = new object[oStackSize];
            constants.CopyTo(oValues);
            ref var position1 = ref iValues[this.position.index];
            position1 = position;
            iValues[this.length.index] = sequence.Length;
            oValues[this.sequence.index] = sequence;
            while (true)
            {
                switch (code[pc].opcode) {
                case OpCode.Jump:
                    pc = code[pc].result;
                    break;
                case OpCode.JumpTrue:
                    if (test) {
                        pc = code[pc].result;
                    }
                    break;
                case OpCode.JumpFalse:
                    if (!test) {
                        pc = code[pc].result;
                    }
                    break;
                case OpCode.IntEqual:
                    test = iValues[code[pc].lhs] == iValues[code[pc].rhs];
                    break;
                case OpCode.IntCompare:
                    test = iValues[code[pc].lhs] < iValues[code[pc].rhs];
                    break;
                case OpCode.ObjEqual:
                    test = oValues[code[pc].lhs] == oValues[code[pc].rhs];
                    break;
                case OpCode.Box:
                    oValues[code[pc].result] = conversions[typeTable[code[pc].lhs]].itoo(iValues[code[pc].lhs]);
                    break;
                case OpCode.Unbox:
                    iValues[code[pc].result] = conversions[typeTable[code[pc].result]].otoi(oValues[code[pc].lhs]);
                    break;
                case OpCode.Call: {
                    var (method, count) = methodTable[code[pc].lhs];
                    var obj = oValues[code[pc].rhs];
                    var args = new object[count];
                    for (int i = 0; i < count; i++) {
                        args[i] = oValues[code[pc].rhs + 1 + i];
                    }
                    oValues[code[pc].result] = method.Invoke(obj, args.ToArray());
                }
                    break;
                case OpCode.Construct: {
                    var (method, count) = methodTable[code[pc].lhs];
                    var args = new object[count];
                    for (int i = 0; i < count; i++) {
                        args[i] = oValues[code[pc].rhs + i];
                    }
                    oValues[code[pc].result] = ((ConstructorInfo)method).Invoke(args);
                }
                    break;
                case OpCode.ObjCopy:
                    oValues[code[pc].result] = oValues[code[pc].lhs];
                    break;
                case OpCode.IntSet:
                    iValues[code[pc].result] = code[pc].lhs;
                    break;
                case OpCode.IntCopy:
                    iValues[code[pc].result] = iValues[code[pc].lhs];
                    break;
                case OpCode.LoadField:
                    oValues[code[pc].result] = fieldTable[code[pc].rhs].GetValue(oValues[code[pc].lhs]);
                    break;
                case OpCode.StoreField:
                    fieldTable[code[pc].rhs].SetValue(oValues[code[pc].lhs], oValues[code[pc].result]);
                    break;
                case OpCode.AddImmediate:
                    iValues[code[pc].result] = iValues[code[pc].lhs] + code[pc].rhs;
                    break;
                case OpCode.Subtract:
                    iValues[code[pc].result] = iValues[code[pc].lhs] - iValues[code[pc].rhs];
                    break;
                case OpCode.Subroutine:
                    var program = dependencies[code[pc].lhs];
                    test = program.Execute(sequence, ref position1, out oValues[code[pc].result]);
                    break;
                case OpCode.Peek:
                    iValues[code[pc].result] = sequence[position1 + code[pc].lhs];
                    break;
                case OpCode.Return:
                    result = oValues[this.result.index];
                    position = position1;
                    return code[pc].lhs != 0;
                }
                pc++;
            }
        }

        public Var Position => position;
        public Var Sequence => sequence;
        public Var Length => length;

        public Var Call(Var? obj, MethodBase method, params Var[] arguments)
        {
            var p = method.GetParameters();
            if (p.Length != arguments.Length) {
                throw new ArgumentException("Parameter count mismatch");
            }
            var mIdx = methodTable.IndexOf((method, p.Length));
            if (mIdx < 0) {
                mIdx = methodTable.Count;
                methodTable.Add((method, p.Length));
            }
            var returnType = (method as MethodInfo)?.ReturnType
                ?? (method as ConstructorInfo)?.DeclaringType
                ?? throw new ArgumentException();
            var result = Allocate(IsInt(returnType) ? typeof(object) : returnType);
            var _args = (method is ConstructorInfo ? Array.Empty<Var>() : new []{obj ?? Default(typeof(object))})
                .Concat(arguments).Cast<RuntimeVar>().ToArray();
            if (_args.Length > 1 && (_args.Length == 0 || IsInt(_args[0]) == IsInt(p[0].ParameterType))) {
                code.Add(new Operation {
                    opcode = OpCode.Call,
                    result = result.index,
                    lhs = mIdx,
                    rhs = _args.Length == 0 ? 0 : _args[0].index,
                });
            } else {
                var start = frames.Peek().allocated.Where(x => !IsInt(x.type)).OrderByDescending(x => x.index).FirstOrDefault()?.index ?? -1;
                start += 1;
                oStackSize = Math.Max(oStackSize, start + p.Length);
                for (int i = 0; i < _args.Length; i++) {
                    code.Add(new Operation {
                        opcode = IsInt(_args[i]) ? OpCode.Box : OpCode.ObjCopy,
                        result = start + i,
                        lhs = _args[i].index,
                    });
                }
                code.Add(new Operation {
                    opcode = method is ConstructorInfo ? OpCode.Construct : OpCode.Call,
                    result = result.index,
                    lhs = mIdx,
                    rhs = start,
                });
            }

            if (IsInt(returnType)) {
                var realResult = Allocate(returnType);
                code.Add(new Operation {
                    opcode = OpCode.Unbox,
                    result = realResult.index,
                    lhs = result.index
                });
                return realResult;
            } else {
                return result;
            }
        }

        public void CallRecursive(Emitter callee, Var? output, Label? onSuccess, Label onFailure)
        {
            var idx = dependencies.IndexOf((Program)callee);
            if (idx < 0) {
                idx = dependencies.Count;
                dependencies.Add((Program)callee);
            }
            output ??= Allocate(((Program)callee).result.type);
            code.Add(new Operation {
                opcode = OpCode.Subroutine,
                result = ((RuntimeVar)output).index,
                lhs = idx
            });
            code.Add(new Operation {
                opcode = OpCode.JumpFalse,
                result = GetTarget(onFailure)
            });
            if (onSuccess != null) {
                code.Add(new Operation {
                    opcode = OpCode.Jump,
                    result = GetTarget(onSuccess)
                });
            }
        }

        public void Compare(Var lhs, CompareOp op, Var rhs, Label label)
        {
            var _lhs = (RuntimeVar)lhs;
            var _rhs = (RuntimeVar)rhs;
            if (IsInt(_lhs) != IsInt(_rhs)) {
                throw new ArgumentException("Type mismatch");
            }
            if (op == CompareOp.Equal || op == CompareOp.NotEqual) {
                code.Add(new Operation {
                    opcode = IsInt(_lhs) ? OpCode.IntEqual : OpCode.ObjEqual,
                    lhs = _lhs.index,
                    rhs = _rhs.index,
                });
                code.Add(new Operation {
                    opcode = op == CompareOp.Equal ? OpCode.JumpTrue : OpCode.JumpFalse,
                    result = GetTarget(label),
                });
                return;
            }
            if (!IsInt(_lhs)) {
                throw new ArgumentException("Can only compare integer values");
            }
            var swap = op == CompareOp.Greater || op == CompareOp.LessOrEqual;
            code.Add(new Operation {
                opcode = OpCode.IntCompare,
                lhs = ((RuntimeVar)(swap ? rhs : lhs)).index,
                rhs = ((RuntimeVar)(swap ? lhs : rhs)).index,
            });
            code.Add(new Operation {
                opcode = (op == CompareOp.GreaterOrEqual || op == CompareOp.LessOrEqual)
                    ? OpCode.JumpFalse : OpCode.JumpTrue,
                result = GetTarget(label)
            });
        }

        public Var Const(object value, Type type)
        {
            if (value == null) {
                throw new ArgumentNullException();
            }
            var v = Allocate(type);
            if (IsInt(v)) {
                code.Add(new Operation {
                    opcode = OpCode.IntSet,
                    lhs = v.index,
                    rhs = conversions[type].otoi(value)
                });
            } else {
                while (constants.Count <= v.index) {
                    constants.Add(null);
                }
                constants[v.index] = value;
            }
            return v;
        }

        public void Copy(Var lhs, Var rhs)
        {
            var _lhs = (RuntimeVar)lhs;
            var _rhs = (RuntimeVar)rhs;
            if (IsInt(_lhs) != IsInt(_rhs)) {
                throw new ArgumentException("Type mismatch");
            }
            code.Add(new Operation {
                opcode = IsInt(_lhs) ? OpCode.IntCopy : OpCode.ObjCopy,
                result = _lhs.index,
                lhs = _rhs.index,
            });
        }

        public Var Difference(Var lhs, Var rhs)
        {
            var _lhs = (RuntimeVar)lhs;
            var _rhs = (RuntimeVar)rhs;
            if (!IsInt(_lhs) || !IsInt(_rhs)) {
                throw new ArgumentException("Both arguments must be integer types");
            }
            var v = Allocate(typeof(int));
            code.Add(new Operation {
                opcode = OpCode.Subtract,
                result = v.index,
                lhs = _lhs.index,
                rhs = _rhs.index
            });
            return v;
        }

        public IDisposable Frame()
        {
            var frame = new StackFrame(this);
            frames.Push(frame);
            return frame;
        }

        public void Increment(Var variable, int amount)
        {
            if (!IsInt((RuntimeVar)variable)) {
                throw new ArgumentException();
            }
            code.Add(new Operation {
                result = ((RuntimeVar)variable).index,
                lhs = ((RuntimeVar)variable).index,
                rhs = amount
            });
        }

        private int GetTarget(Label label)
        {
            var r = (RuntimeLabel)label;
            if (r.target < 0) {
                r.jumpPoints.Add(code.Count);
            }
            return r.target;
        }

        public void Jump(Label label)
        {
            code.Add(new Operation {
                opcode = OpCode.Jump,
                result = GetTarget(label)
            });
        }

        public Label Label() => new RuntimeLabel();

        public Var Load(Var obj, MemberInfo member)
        {
            if (member is FieldInfo field) {
                var fIdx = fieldTable.IndexOf(field);
                if (fIdx < 0) {
                    fIdx = fieldTable.Count;
                    fieldTable.Add(field);
                }
                var v = Allocate(field.FieldType);
                if (IsInt(v)) {
                    using var _ = Frame();
                    var v1 = Allocate(typeof(object));
                    code.Add(new Operation {
                        opcode = OpCode.LoadField,
                        result = v1.index,
                        lhs = ((RuntimeVar)obj).index,
                        rhs = fIdx
                    });
                    code.Add(new Operation {
                        opcode = OpCode.Unbox,
                        result = v.index,
                        lhs = v1.index
                    });
                } else {
                    code.Add(new Operation {
                        opcode = OpCode.LoadField,
                        result = v.index,
                        lhs = ((RuntimeVar)obj).index,
                        rhs = fIdx
                    });
                }
                return v;
            } else if (member is PropertyInfo prop) {
                return Call(obj, prop.GetGetMethod());
            } else {
                throw new ArgumentException();
            }
        }

        internal static Context MakeRoot(Type outputType, IEnumerable<Feature> features)
        {
            var p = new Program(outputType, features);
            var skip = p.Label();
            p.Jump(skip);
            var success = p.Label();
            p.Mark(success);
            p.code.Add(new Operation {
                opcode = OpCode.Return,
                lhs = 1
            });
            var failure = p.Label();
            p.Mark(failure);
            p.code.Add(new Operation {
                opcode = OpCode.Return,
                rhs = 0
            });
            p.Mark(skip);
            return new Context(p, p.result, success, failure, null, features, true);
        }

        public Context MakeRecursive(Type outputType) => MakeRoot(outputType, features);

        public void Mark(Label label) {
            var r = (RuntimeLabel)label;
            r.target = code.Count - 1; // Go to previous instruction because of pc++
            foreach (var pc in r.jumpPoints) {
                switch (code[pc].opcode) {
                    case OpCode.Jump:
                    case OpCode.JumpFalse:
                    case OpCode.JumpTrue:
                        var inst = code[pc];
                        inst.result = r.target;
                        code[pc] = inst;
                        break;
                    default:
                        throw new InvalidOperationException("Label applied to non-jump operation");
                }
            }
            r.jumpPoints.Clear();
        }

        private RuntimeVar Allocate(Type type) {
            var found = pool.FirstOrDefault(x => x.type == type);
            if (found == null) {
                found = new RuntimeVar(type, IsInt(type) ? iStackSize++ : oStackSize++, false);
            } else {
                pool.Remove(found);
            }
            frames.Peek().allocated.Add(found);
            return found;
        }

        public Var Peek(int offset)
        {
            var v = Allocate(typeof(char));
            code.Add(new Operation {
                opcode = OpCode.Peek,
                lhs = offset,
                result = v.index
            });
            return v;
        }

        public void Set(Var variable, object? value)
        {
            var rVar = (RuntimeVar)variable;
            if (conversions.TryGetValue(rVar.type, out var conv)) {
                code.Add(new Operation {
                    opcode = OpCode.IntSet,
                    result = rVar.index,
                    lhs = value == null ? 0 : conv.otoi(value),
                });
            } else {
                var c = (RuntimeVar)(value == null ? Default(rVar.type) : Const(value, rVar.type));
                code.Add(new Operation {
                    opcode = OpCode.ObjCopy,
                    result = rVar.index,
                    lhs = c.index,
                });
            }
        }

        public void Store(Var obj, MemberInfo member, Var value)
        {
            if (member is FieldInfo field) {
                var fIdx = fieldTable.IndexOf(field);
                if (fIdx < 0) {
                    fIdx = fieldTable.Count;
                    fieldTable.Add(field);
                }
                using var _ = Frame();
                var _value = (RuntimeVar)value;
                if (IsInt(_value)) {
                    var v1 = Allocate(typeof(object));
                    code.Add(new Operation {
                        opcode = OpCode.Box,
                        lhs = _value.index,
                        rhs = v1.index
                    });
                    _value = v1;
                }
                code.Add(new Operation {
                    opcode = OpCode.StoreField,
                    result = _value.index,
                    lhs = ((RuntimeVar)obj).index,
                    rhs = fIdx
                });
            } else if (member is PropertyInfo prop) {
                Call(obj, prop.GetSetMethod(), value);
            } else {
                throw new ArgumentException();
            }
        }

        public Type TypeOf(Var stackSlot) => ((RuntimeVar)stackSlot).type;

        public Var Var(object? initial, Type type) {
            var v = Allocate(type);
            Set(v, initial);
            return v;
        }

        public Var Default(Type type)
        {
            return new RuntimeVar(type, 0, true);
        }
    }
}
