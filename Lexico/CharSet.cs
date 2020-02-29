using System.Linq;
using System;
using static System.Linq.Expressions.Expression;
using System.Reflection;
using System.Collections.Generic;

namespace Lexico
{
    public class CharSetAttribute : TermAttribute
    {
        public CharSetAttribute(string set) {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        private readonly string set;

        public override IParser Create(MemberInfo member, Func<IParser> child, IConfig config) => new CharSet(set);
    }

    public class CharIntervalSet
    {
        private readonly List<(char, char)> intervals = new List<(char, char)>();

        public void All() {
            intervals.Clear();
            intervals.Add(('\u0000', '\uffff'));
        }

        public void Clear() {
            intervals.Clear();
        }

        public void Include(char begin, char end) {
            int i;
            bool bc = false;
            for (i = 0; i < intervals.Count; i++) {
                if (begin < intervals[i].Item1) {
                    bc = false;
                    break;
                }
                if (begin >= intervals[i].Item1 && begin <= intervals[i].Item2) {
                    bc = true;
                    break;
                }
            }
            int j;
            bool ec = false;
            for (j = 0; j < intervals.Count; j++) {
                if (end < intervals[j].Item1) {
                    ec = false;
                    break;
                }
                if (end >= intervals[j].Item1 && end <= intervals[j].Item2) {
                    ec = true;
                    break;
                }
            }
            switch (bc, ec) {
            case (true, true):
                // Both are within existing intervals; remove any gaps in between
                var newEnd = intervals[j].Item2;
                var newStart = intervals[i].Item1;
                for (int p = i+1; p <= j; p++) {
                    intervals.RemoveAt(p);
                }
                intervals[i] = (newStart, newEnd);
                break;
            case (false, false):
                // Not contained - remove any obsolete intervals in the middle:
                for (int p = i; p < j; p++) {
                    intervals.RemoveAt(p);
                }
                // Add a new interval covering everything
                intervals.Insert(i, (begin, end));
                break;
            case (true, false):
                for (int p = i+1; p <= j; p++) {
                    intervals.RemoveAt(p);
                }
                intervals[i] = (intervals[i].Item1, end);
                break;
            case (false, true):
                var newEnd1 = intervals[j].Item2;
                for (int p = i+1; p <= j; p++) {
                    intervals.RemoveAt(p);
                }
                intervals[i] = (begin, newEnd1);
                break;
            }
        }

        public void Invert()
        {
            var newSet = new List<(char, char)>();
            var pos = '\0';
            foreach (var (begin, end) in intervals) {
                if (begin > pos) {
                    newSet.Add((pos, (char)(begin-1)));
                }
                pos = (char)(end+1);
            }
        }

        public void Exclude(char begin, char end) {

        }
    }

    public class CharSet : IParser
    {
        public CharSet(string set) {
            set = set ?? throw new ArgumentNullException(nameof(set));
            chars = set.Distinct().ToArray();
            if (chars.Length == 0) {
                throw new ArgumentException("Char set is empty");
            }
        }

        public Type OutputType => typeof(char);

        private readonly char[] chars;

        public void Compile(ICompileContext context)
        {
            context.Append(IfThen(GreaterThanOrEqual(context.Position, context.Length), Goto(context.Failure)));
            var success = Label();
            foreach (var c in chars) {
                context.Append(IfThen(Equal(context.Peek(0), Constant(c)), Goto(success)));
            }
            context.Fail();
            context.Append(Label(success));
            context.Advance(1);
            context.Succeed(context.Peek(0));
        }
    }
}