#pragma warning disable CS0169,CS0649,IDE0044,IDE0051
using System;
using Lexico;

namespace Calculator
{
    public abstract class Expression
    {
        public abstract float Value { get; }
    }

   // [WhitespaceSeparated]
    public abstract class BinaryExpression : Expression
    {
        protected Expression lhs;
        [IndirectLiteral(nameof(Operator))] protected Unnamed _;
        protected Expression rhs;

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
        float value;
        public override float Value => value;
    }

   // [WhitespaceSeparated, WhitespaceSurrounded]
    public class Bracketed : Expression
    {
        [Literal("(")] Unnamed _;
        Expression inner;
        [Literal(")")] Unnamed __;

        public override float Value => inner.Value;
    }
}