using System.Text;
using System.Reflection;
using System.Collections;
using System;
using System.Collections.Generic;
using static System.Linq.Expressions.Expression;
using System.Linq.Expressions;

namespace Lexico
{
    /// <summary>
    /// Matches an element repeatedly (at least one time). Can be applied to any IList`1 type, or any generic
    /// type with a compatible Add method (such as HashSet`1). Array members must be settable; others can be modified in-place.
    /// Applied by default to any ICollection.
    /// </summary>
    public class RepeatAttribute : TermAttribute
    {
        /// <summary>
        /// The minimum number of elements to match to succeed
        /// </summary>
        /// <value></value>
        public int Min { get; set; } = 1;

        /// <summary>
        /// The maximum number of elements to match (the parser will simply stop when it reaches this amount)
        /// </summary>
        /// <value></value>
        public int Max { get; set; } = Int32.MaxValue;

        public override int Priority => 20;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config)
            => new RepeatParser(member.GetMemberType(), null,
            Min > 0 ? Min : default(int?), Max < Int32.MaxValue ? Max : default(int?));

        public override bool AddDefault(MemberInfo member)
            => member is Type t && typeof(ICollection).IsAssignableFrom(t);
    }

    internal class RepeatParser : IParser
    {
        public RepeatParser(Type listType, IParser? separator, int? min, int? max)
        {
            OutputType = listType ?? throw new ArgumentNullException(nameof(listType));
            var elementType = listType switch {
                {} when listType == typeof(string) => typeof(char),
                {IsArray: true} => listType.GetElementType(),
                _ => listType.GetGenericArguments()[0]
            };
            Element = ParserCache.GetParser(elementType);
            if (listType != typeof(string)
                && !typeof(IList).IsAssignableFrom(listType)
                && listType.GetMethod("Add") == null) {
                throw new ArgumentException($"{listType} does not implement IList and has no Add method");
            }
            this.separator = separator;
            Min = min;
            Max = max;
        }

        public RepeatParser(Type outputType, IParser element, IParser? separator, int? min, int? max)
        {
            OutputType = outputType ?? throw new ArgumentNullException(nameof(outputType));
            Element = element;
            this.separator = separator;
            Min = min;
            Max = max;
        }

        public int? Min { get; }
        public int? Max { get; }
        public IParser Element { get; }
        private readonly IParser? separator;

        public Type OutputType { get; }

        public void Compile(ICompileContext context)
        {
            var list = context.Result == null ? null :
                context.Cache(OutputType switch {
                    {} when OutputType == typeof(string) => New(typeof(StringBuilder)),
                    {IsArray: true} => New(typeof(List<>).MakeGenericType(Element.OutputType)),
                    _ => Condition(Equal(context.Result, Constant(null)), New(OutputType), context.Result)
                });
            if (list != null && OutputType != typeof(string)) {
                context.Append(Call(list, nameof(IList.Clear), Type.EmptyTypes));
            }
            // Make a var to store the result before adding to the list
            var output = context.Result == null ? null : context.Cache(Default(Element.OutputType));
            var count = context.Cache(Constant(0));
            void AddToList() {
                if (list != null) {
                    if (OutputType == typeof(string)) {
                        context.Append(Call(list, nameof(StringBuilder.Append), Type.EmptyTypes, output));
                    } else {
                        context.Append(Call(list, nameof(IList.Add), Type.EmptyTypes, output));
                    }
                }
            }

            // Begin the loop
            var loop = Label();
            var loopEnd = Label();

            // First element
            context.Child(Element, null, output, null, loopEnd);
            AddToList();

            context.Append(Label(loop));
            if (output != null) {
                context.Append(Assign(output, Default(Element.OutputType)));
            }
            if (Max.HasValue) {
                context.Append(IfThen(GreaterThanOrEqual(count, Constant(Max.Value)), Goto(loopEnd)));
            }
            // Subsequent elements can fail
            var loopFail = context.Save();
            if (separator != null) {
                context.Child(separator, "(Separator)", null, null, loopFail);
            }
            context.Child(Element, null, output, null, loopFail);
            AddToList();
            context.Append(Goto(loop));
            context.Restore(loopFail);
            context.Append(Label(loopEnd));

            // Loop ends; decide whether to succeed or not
            if (Min.HasValue) {
                context.Append(IfThen(LessThan(count, Constant(Min.Value)), Goto(context.Failure)));
            }
            if (OutputType == typeof(string) && list != null) {
                context.Succeed(Call(list, nameof(StringBuilder.ToString), Type.EmptyTypes));
            } else {
                context.Succeed(list ?? Empty());
            }
        }

        public override string ToString() => $"[{Element}...]";
    }
}