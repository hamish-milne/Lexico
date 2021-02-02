using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Lexico
{
    public class CharSetAttribute : TermAttribute
    {
        public CharSetAttribute(string set) {
            this.set = new CharIntervalSet();
            foreach (var c in set) {
                this.set.Include(c, c);
            }
        }
        public override int Priority => 10;

        private readonly CharIntervalSet set;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new CharSet(set);
    }

    public class CharRangeAttribute : TermAttribute
    {
        public CharRangeAttribute(params string[] ranges) {
            var r = ranges.Select(s => {
                if (s.Length != 2) throw new ArgumentException($"Argument `{s}` in CharRange is not 2 characters");
                return (s[0], s[1]);
            });
            set = new CharIntervalSet();
            foreach (var (begin, end) in r) {
                set.Include(begin, end);
            }
        }

        private readonly CharIntervalSet set;

        public override IParser Create(MemberInfo member, ChildParser child, IConfig config) => new CharSet(set);
    }

    public class CharIntervalSet
    {
        private readonly List<(char, char)> intervals = new List<(char, char)>();

        public IEnumerable<(char, char)> Intervals => intervals;

        public CharIntervalSet() {}

        public CharIntervalSet(IEnumerable<CharIntervalSet> others)
        {
            foreach (var o in others) {
                foreach (var (begin, end) in o.intervals) {
                    Include(begin, end);
                }
            }
        }

        public CharIntervalSet All() {
            intervals.Clear();
            intervals.Add(('\u0000', '\uffff'));
            return this;
        }

        public void Clear() {
            intervals.Clear();
        }

        private (bool, int) Find(char c) {
            int i;
            bool contains = false;
            for (i = 0; i < intervals.Count; i++) {
                if (c < intervals[i].Item1) {
                    contains = false;
                    break;
                }
                if (c >= intervals[i].Item1 && c <= intervals[i].Item2) {
                    contains = true;
                    break;
                }
            }
            return (contains, i);
        }

        public CharIntervalSet Include(char begin, char end) {
            var (bc, i) = Find(begin);
            var (ec, j) = Find(end);

            var newEnd = ec ? intervals[j].Item2 : end;
            var newStart = bc ? intervals[i].Item1 : begin;
            // Remove redundant intervals between the added endpoints
            var s = i + (bc || ec ? 1 : 0);
            for (int p = s; p < j; p++) {
                intervals.RemoveAt(s);
            }
            if (!bc && !ec) {
                // If no endpoint was contained, add a brand new interval in the right place
                intervals.Insert(i, (begin, end));
            } else {
                // Otherwise, extend an existing interval
                intervals[i] = (newStart, newEnd);
            }
            return this;
        }

        public CharIntervalSet Exclude(char begin, char end)
        {
            var (bc, i) = Find(begin);
            var (ec, j) = Find(end);

            if (bc) {
                intervals[i] = (intervals[i].Item1, begin);
            }
            if (ec) {
                intervals[j] = (end, intervals[j].Item2);
            }
            var s = i+(bc || ec ? 1 : 0);
            for (int p = s; p < j; p++) {
                intervals.RemoveAt(s);
            }
            return this;
        }

        public CharIntervalSet Invert()
        {
            var newSet = new List<(char, char)>();
            var pos = '\0';
            foreach (var (begin, end) in intervals) {
                if (begin > pos) {
                    newSet.Add((pos, (char)(begin-1)));
                }
                pos = (char)(end+1);
            }
            if (pos <= '\uffff') {
                newSet.Add((pos, '\uffff'));
            }
            intervals.Clear();
            intervals.AddRange(newSet);
            return this;
        }

        public override string ToString() => string.Join(",", intervals.Select(t => $"{t.Item1}-{t.Item2}"));
    }

    public class CharSet : IParser
    {
        public CharSet(CharIntervalSet set) {
            ranges = set.Intervals.ToArray();
        }

        private readonly (char start, char end)[] ranges;

        public Type OutputType => typeof(char);

        public void Compile(Context context)
        {
            context.PopCachedResult();
            var e = context.Emitter;
            context.RequireSymbols(1);
            var success = e.Label();
            context.Peek(0);
            foreach (var (start, end) in ranges) {
                if (start == end) {
                    e.Dup();
                    e.Const(start);
                    e.Jump(CMP.Equal, success);
                } else {
                    e.Dup();
                    e.Const(start);
                    e.Compare(CMP.GreaterOrEqual);
                    context.Peek(0);
                    e.Const(end);
                    e.Compare(CMP.LessOrEqual);
                    e.Operate(BOP.And);
                    e.Jump(true, success);
                }
            }
            e.Pop();
            e.Jump(context.Failure);
            e.Mark(success);
            if (!context.HasResult()) {
                e.Pop();
            }
            context.Advance(1);
            context.Succeed();
        }

        // TODO: Use single chars for ToString
        public override string ToString() => string.Join(",", ranges.Select(t => $"{t.start}-{t.end}"));
    }
}