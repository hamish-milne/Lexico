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
    }

    internal class RepeatParser : IParser
    {
        public RepeatParser(Type listType, IParser? separator)
        {
            this.listType = listType ?? throw new ArgumentNullException(nameof(listType));
            element = ParserCache.GetParser(listType.IsArray ? listType.GetElementType() : listType.GetGenericArguments()[0]);
            if (!typeof(IList).IsAssignableFrom(listType)) {
                addMethod = listType.GetMethod("Add")
                    ?? throw new ArgumentException($"{listType} does not implement IList and has no Add method");
            }
            this.separator = separator;
        }

        public static RepeatParser Modify(RepeatParser parent, MemberInfo member)
        {
            var sep = member.GetCustomAttribute<SeparatedByAttribute>(true);
            if (sep != null) {
                return new RepeatParser(parent.listType, ParserCache.GetParser(sep.Separator, "separator"));
            } else {
                return parent;
            }
        }
        private readonly Type listType;
        private readonly IParser element;
        private readonly MethodInfo addMethod;
        private readonly IParser? separator;
        public bool Matches(ref Buffer buffer, ref object value, ITrace trace)
        {
            if (listType.IsArray) {
                value = new List<object>();
            }else if (value == null) {
                value = Activator.CreateInstance(listType);
            }
            var list = value as IList;
            var args = new object[1];
            var listObj = value;

            void AddItem(object obj) {
                list?.Add(obj);
                args[0] = obj;
                addMethod?.Invoke(listObj, args);
            }

            list?.Clear();
            object evalue = null;
            if (!element.Matches(ref buffer, ref evalue, trace)) {
                return false;
            }
            AddItem(evalue);
            int lastSuccessPos = buffer.Position;
            do {
                evalue = null;
                object tmp = null;
                if (separator?.Matches(ref buffer, ref tmp, trace) == false) {
                    break;
                }
                if (!element.Matches(ref buffer, ref evalue, trace)) {
                    break;
                }
                AddItem(evalue);
                lastSuccessPos = buffer.Position;
            } while (true);
            buffer.Position = lastSuccessPos;
            if (listType.IsArray)
            {
                var newarr = Array.CreateInstance(listType.GetElementType(), list!.Count);
                list!.CopyTo(newarr, 0);
                value = newarr;
            }
            return true;
        }
    }
}