using System.Reflection;
using System.Collections;
using System;
using System.Collections.Generic;
using static System.AttributeTargets;

namespace Lexico
{
    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = false)]
    public class SeparatedByAttribute : TermAttribute
    {
        public SeparatedByAttribute(Type separator) {
            Separator = separator ?? throw new ArgumentNullException(nameof(separator));
        }
        public Type Separator { get; }

        public override IParser Create(MemberInfo member, Func<IParser> child)
        {
            var c = child();
            var sep = ParserCache.GetParser(Separator);
            return c switch {
                RepeatParser r => new RepeatParser(r.ListType, sep, r.Min, r.Max),
                SequenceParser s => new SequenceParser(s.Type, sep),
                _ => throw new ArgumentException($"Separator not valid on {c}")
            };
        }
    }

    public class RepeatAttribute : TermAttribute
    {
        public int Min { get; set; } = 0;
        public int Max { get; set; } = Int32.MaxValue;
        public override int Priority => 20;
        public override IParser Create(MemberInfo member, Func<IParser> child)
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