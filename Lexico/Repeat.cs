using System.Text;
using System.Reflection;
using System.Collections;
using System;
using System.Collections.Generic;

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

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config)
        {
            var listType = member.GetMemberType() ?? throw new ArgumentException();
            var elementType = listType switch {
                {} when listType == typeof(string) => typeof(char),
                {IsArray: true} => listType.GetElementType(),
                _ => listType.GetGenericArguments()[0]
            };
            return new RepeatParser(member.GetMemberType(), child(elementType), null,
                Min > 0 ? Min : default(int?), Max < Int32.MaxValue ? Max : default(int?));
        }

        public override bool AddDefault(MemberInfo member)
            => member is Type t && typeof(ICollection).IsAssignableFrom(t);
    }

    internal class RepeatParser : IParser
    {
        public RepeatParser(Type outputType, IParser element, IParser? separator, int? min, int? max)
        {
            if (outputType != typeof(string)
                && !typeof(IList).IsAssignableFrom(outputType)
                && outputType.GetMethod("Add") == null) {
                throw new ArgumentException($"{outputType} does not implement IList and has no Add method");
            }
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

        public void Compile(Context context)
        {
            var e = context.Emitter;
            Var? list = null;
            if (context.Result != null) {
                if (context.CanWriteResult) {
                    if (OutputType == typeof(string)) {
                        list = e.Create(typeof(StringBuilder));
                    } else if (OutputType.IsArray) {
                        list = e.Create(typeof(List<>).MakeGenericType(Element.OutputType));
                    } else {
                        list = context.Result;
                        var skip = e.Label();
                        e.Compare(list, CompareOp.NotEqual, e.Default(typeof(object)), skip);
                        e.Copy(list, e.Create(OutputType));
                        e.Mark(skip);
                    }
                } else if (typeof(IList).IsAssignableFrom(OutputType)) {
                    list = context.Result;
                }
            }
            if (list != null && OutputType != typeof(string)) {
                e.Call(list, nameof(IList.Clear));
            }
            // Make a var to store the result before adding to the list
            var output = context.Result == null ? null : e.Var(null, Element.OutputType);
            var count = e.Var(0, typeof(int));
            void AddToList() {
                if (list != null) {
                    if (OutputType == typeof(string)) {
                        e.Call(list, nameof(StringBuilder.Append), output!);
                    } else {
                        e.Call(list, nameof(IList.Add), output!);
                    }
                }
                if (Min.HasValue || Max.HasValue) {
                    e.Increment(count, 1);
                }
            }

            // Begin the loop
            var loop = e.Label();
            var loopEnd = e.Label();

            // First element
            context.Child(Element, null, output, null, loopEnd);
            AddToList();

            e.Mark(loop);
            if (output != null) {
                e.Copy(output, e.Default(Element.OutputType));
            }
            if (Max.HasValue) {
                e.Compare(count, CompareOp.GreaterOrEqual, e.Const(Max.Value), loopEnd);
            }
            // Subsequent elements can fail
            var loopFail = context.Save();
            if (separator != null) {
                context.Child(separator, "(Separator)", null, null, loopFail.label);
            }
            if (output != null) {
                e.Copy(output, e.Default(Element.OutputType));
            }
            context.Child(Element, null, output, null, loopFail.label);
            AddToList();
            e.Jump(loop);
            context.Restore(loopFail);
            e.Mark(loopEnd);

            // Loop ends; decide whether to succeed or not
            if (Min.HasValue) {
                e.Compare(count, CompareOp.Less, e.Const(Min.Value), context.Failure);
            }
            if (OutputType == typeof(string) && list != null) {
                context.Succeed(e.Call(list, nameof(StringBuilder.ToString)));
            } else {
                context.Succeed(list);
            }
        }

        public override string ToString() => $"[{Element}...]";
    }
}