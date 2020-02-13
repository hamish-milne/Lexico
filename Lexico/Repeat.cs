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
        public int Min { get; set; } = 0;

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
            this.ListType = listType ?? throw new ArgumentNullException(nameof(listType));
            element = ParserCache.GetParser(listType.IsArray ? listType.GetElementType() : listType.GetGenericArguments()[0]);
            if (!typeof(IList).IsAssignableFrom(listType)) {
                addMethod = listType.GetMethod("Add")
                    ?? throw new ArgumentException($"{listType} does not implement IList and has no Add method");
            }
            this.separator = separator;
            Min = min;
            Max = max;
        }

        public Type ListType { get; }
        public int? Min { get; }
        public int? Max { get; }
        private readonly IParser element;
        private readonly MethodInfo? addMethod;
        private readonly IParser? separator;
        public bool Matches(ref IContext context, ref object? value)
        {
            if (ListType.IsArray) {
                value = new List<object>();
            } else if (value == null) {
                value = Activator.CreateInstance(ListType);
            }
            var list = value as IList;
            var args = new object?[1];
            var listObj = value;
            int count = 0;

            void AddItem(object? obj) {
                list?.Add(obj);
                args[0] = obj;
                addMethod?.Invoke(listObj, args);
                count++;
            }

            list?.Clear();
            object? evalue = null;
            if (!element.MatchChild(null, ref context, ref evalue)) {
                return false;
            }
            AddItem(evalue);
            var lastSuccessPos = context;
            do {
                evalue = null;
                object? tmp = null;
                if (!separator.MatchChild("(Separator)", ref context, ref tmp)) {
                    break;
                }
                if (!element.MatchChild(null, ref context, ref evalue)) {
                    break;
                }
                AddItem(evalue);
                lastSuccessPos = context;
            } while (!Max.HasValue || count < Max);
            context = lastSuccessPos;
            if (Min.HasValue && count < Min) {
                return false;
            }
            if (ListType.IsArray)
            {
                var newarr = Array.CreateInstance(ListType.GetElementType(), list!.Count);
                list!.CopyTo(newarr, 0);
                value = newarr;
            }
            return true;
        }
    }
}