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
            Min = min ?? 1;
            Max = max;
        }

        public int Min { get; }
        public int? Max { get; }
        public IParser Element { get; }
        private readonly IParser? separator;

        public Type OutputType { get; }

        public void Compile(Context context)
        {
            var e = context.Emitter;
            Type listType;
            if (OutputType == typeof(string)) {
                listType = typeof(StringBuilder);
            } else if (OutputType.IsArray) {
                // TODO: Does this work on AOT?
                listType = typeof(List<>).MakeGenericType(Element.OutputType);
            } else {
                listType = OutputType;
            }
            switch (context.Result) {
            case ResultMode.Mutate:
                if (listType != OutputType) {
                    throw new Exception("Repeat requires a mutable list type or a writable field/property");
                } else {
                    e.Dup();
                    e.Call(listType.GetMethod(nameof(IList.Clear)));
                }
                break;
            case ResultMode.Modify:
                if (listType != OutputType) {
                    e.Pop();
                    e.Create(listType);
                } else {
                    e.Dup();
                    using (e.If(CMP.IsNull)) {
                        e.Pop();
                        e.Create(OutputType);
                    }
                }
                break;
            case ResultMode.Output:
                e.Create(listType);
                break;
            }

            var count = e.Local(typeof(int));
            // Begin the loop
            var loop = e.Label();
            var loopEnd = e.Label();
            var startLoop = e.Label();

            e.Jump(startLoop);
            e.Mark(loop);
            if (Max.HasValue) {
                e.Load(count);
                e.Const(Max.Value);
                e.Jump(CMP.GreaterOrEqual, loopEnd);
            }
            // Subsequent elements can fail
            var loopFail = context.Save();
            if (separator != null) {
                context.Child(separator, "(Separator)", ResultMode.None, null, loopFail.label);
            }
            e.Mark(startLoop);
            if (context.HasResult()) {
                e.Dup();
            }
            context.Child(Element, null, ResultMode.Output, null, loopFail.label);
            if (context.HasResult()) {
                if (OutputType == typeof(string)) {
                    e.Call(typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append)));
                } else {
                    e.Call(listType.GetMethod(nameof(IList.Add)));
                }
            }
            e.Increment(count, 1);
            e.Jump(loop);
            context.Restore(loopFail);
            e.Mark(loopEnd);

            // Loop ends; decide whether to succeed or not
            e.Load(count);
            e.Const(Min);
            using (e.If(CMP.Less)) {
                if (context.HasResult()) {
                    e.Pop();
                }
                context.Fail();
            }
            if (context.HasResult()) {
                if (OutputType == typeof(string)) {
                    e.Call(typeof(StringBuilder).GetMethod(nameof(StringBuilder.ToString)));
                } else if (OutputType.IsArray) {
                    e.Call(listType.GetMethod(nameof(List<object>.ToArray)));
                }
            }
            context.Succeed();
        }

        public override string ToString() => $"[{Element}...]";
    }
}