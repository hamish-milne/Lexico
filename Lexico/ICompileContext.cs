using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
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

    internal delegate bool Parser<T>(string input, ref int position, ref T value, ITrace trace);
}