using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    public interface ICompileContext
    {
        LabelTarget Save();
        void Release(LabelTarget target);
        void Restore(LabelTarget savePoint);
        LabelTarget? Success { get; }
        LabelTarget Failure { get; }
        Expression Position { get; }
        Expression? Result { get; }
        Expression Length { get; }
        Expression? Cut { get; }
        Expression Cache(Expression value);
        void Release(Expression variable);
        Expression String { get; }
        Expression UserObject { get; }
        void Append(Expression statement);
        void Child(IParser child, string? name, Expression? result, LabelTarget? onSuccess, LabelTarget onFail, Expression? cut = null);
        void Recursive(IParser child);
    }

    public static class ContextExtensions
    {
        public static void Succeed(this ICompileContext context, Expression value)
        {
            if (context.Result?.CanWrite() == true) {
                context.Append(Assign(context.Result, Convert(value, context.Result.Type)));
            }
            context.Succeed();
        }
        // TODO: Check that succeed and fail are both called by child contexts?
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

    internal delegate bool Parser<T>(string input, ref int position, ref T value, ITrace trace, object? userObject);
}