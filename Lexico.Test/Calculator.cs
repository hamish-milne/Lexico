#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System;

namespace Lexico.Calculator
{
    [CompileFlags(CompileFlags.CheckImmediateLeftRecursion | CompileFlags.AggressiveMemoizing)]
    public abstract class Expression
    {
        public abstract float Value { get; }

        public override string ToString() => Value.ToString();
    }

   // [WhitespaceSeparated]
    public abstract class BinaryExpression : Expression
    {
        [Term] protected Expression lhs;
        [IndirectLiteral(nameof(Operator))] protected Unnamed _;
        [Term] protected Expression rhs;

        protected abstract string Operator { get; }
    }

    public class Subtract : BinaryExpression
    {
        protected override string Operator => "-";
        public override float Value => lhs.Value - rhs.Value;
    }

    public class Add : BinaryExpression
    {
        protected override string Operator => "+";
        public override float Value => lhs.Value + rhs.Value;
    }

    public class Multiply : BinaryExpression
    {
        protected override string Operator => "*";
        public override float Value => lhs.Value * rhs.Value;
    }

    public class Divide : BinaryExpression
    {
        protected override string Operator => "/";
        public override float Value => lhs.Value / rhs.Value;
    }

    public class Power : BinaryExpression
    {
        protected override string Operator => "^";
        public override float Value => (float)Math.Pow(lhs.Value, rhs.Value);
    }

  //  [WhitespaceSurrounded]
    public class Number : Expression
    {
        [Term] float value;
        public override float Value => value;
    }

   // [WhitespaceSeparated, WhitespaceSurrounded]
    public class Bracketed : Expression
    {
        [Literal("(")] Unnamed _;
        [Term] Expression inner;
        [Literal(")")] Unnamed __;

        public override float Value => inner.Value;
    }
}