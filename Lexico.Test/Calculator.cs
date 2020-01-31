using Lexico;

namespace Calculator
{
    public abstract class Expression
    {
        public abstract float Value { get; }
    }

    public class Bracketed : Expression
    {
        [Literal("(")] Unnamed _;
        Expression inner;
        [Literal(")")] Unnamed __;

        public override float Value => inner.Value;
    }

    public class Add : Expression
    {
        Expression lhs;
        [Literal("+")] Unnamed _;
        Expression rhs;

        public override float Value => lhs.Value + rhs.Value;
    }

    public class Number : Expression
    {
        float value;
        public override float Value => value;
    }
}