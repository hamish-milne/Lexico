using System;

namespace Lexico
{
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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class CompileFlagsAttribute : Attribute
    {
        public CompileFlagsAttribute(CompileFlags value) => Value = value;
        public CompileFlags Value { get; }
    }
}
