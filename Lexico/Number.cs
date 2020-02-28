using System.Collections.Generic;
using System.Text;
using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Globalization.NumberStyles;
using static System.Linq.Expressions.Expression;

namespace Lexico
{
    /// <summary>
    /// Matches an Arabic numeral string. The exact form depends on the member type and/or NumberStyles config.
    /// For example 'float' members allow decimal points, but 'int' members do not.
    /// Applied by default to all built-in arithmetic types.
    /// </summary>
    public class NumberAttribute : TerminalAttribute
    {
        public override IParser Create(MemberInfo member, IConfig config)
        {
            // TODO: Consider caching these
            return new NumberParser(
                config.Get(defaultNumbers[member.GetMemberType()]),
                member.GetMemberType());
        }

        private static readonly Dictionary<Type, NumberStyles> defaultNumbers
            = new Dictionary<Type, NumberStyles>
        {
            {typeof(int), Integer},
            {typeof(long), Integer},
            {typeof(short), Integer},
            {typeof(sbyte), Integer},
            {typeof(uint), None},
            {typeof(ulong), None},
            {typeof(ushort), None},
            {typeof(byte), None},
            {typeof(decimal), Float},
            {typeof(float), Float},
            {typeof(double), Float},
        };

        public override bool AddDefault(MemberInfo member)
            => member.GetMemberType() is Type t && defaultNumbers.ContainsKey(t);
    }

    internal class NumberParser : IParser
    {
        public NumberParser(NumberStyles styles, Type numberType)
        {
            parseMethod = numberType.GetMethod(nameof(int.Parse), new []{typeof(string), typeof(NumberStyles)})
                ?? throw new ArgumentException($"{numberType} has no Parse method");
            // TODO: Able to set CultureInfo?
            var formatInfo = CultureInfo.InvariantCulture.NumberFormat;
            var pattern = new StringBuilder("^");
            bool Has(NumberStyles ns) => (styles & ns) != 0;
            if (Has(AllowThousands)) {
                throw new NotSupportedException(AllowThousands.ToString());
            }
            if (Has(AllowLeadingWhite)) {
                pattern.Append(@"\s*");
            }
            if (Has(AllowLeadingSign)) {
                pattern.Append(@"[\-\+]?");
            }
            if (Has(AllowCurrencySymbol)) {
                pattern.Append($"(?>{Regex.Escape(formatInfo.CurrencySymbol)}?");
            }
            if (Has(AllowParentheses)) {
                pattern.Append(@"((?'Open'\())?");
            }
            if (Has(AllowHexSpecifier)) {
                pattern.Append(@"[0-9A-Fa-f]+");
            } else {
                if (Has(AllowDecimalPoint)) {
                    pattern.Append(@"[0-9]*\.?");
                }
                pattern.Append(@"[0-9]+");
            }
            if (Has(AllowExponent)) {
                pattern.Append(@"(?>[eE][\-\+]?[0-9]+)?");
            }
            if (Has(AllowParentheses)) {
                pattern.Append(@"(?(Open)\)|)");
            }
            if (Has(AllowTrailingSign)) {
                pattern.Append(@"[\-\+]?");
            }
            if (Has(AllowTrailingWhite)) {
                pattern.Append(@"\s*");
            }
            regex = new Regex(pattern.ToString(), RegexOptions.Compiled);
            this.styles = styles;
        }

        private readonly NumberStyles styles;
        private readonly MethodInfo parseMethod;
        private readonly Regex regex;

        public Type OutputType => parseMethod.DeclaringType;

        public void Compile(ICompileContext context)
        {
            var match = context.Cache(Call(
                Constant(regex),
                nameof(Regex.Match), Type.EmptyTypes,
                context.String,
                context.Position,
                Subtract(context.Length, context.Position)
            ));
            context.Append(IfThen(Not(PropertyOrField(match, nameof(Match.Success))), Goto(context.Failure)));
            context.Append(AddAssign(context.Position, PropertyOrField(match, nameof(Match.Length))));
            context.Succeed(Call(parseMethod, PropertyOrField(match, nameof(Match.Value)), Constant(styles)));
        }

        public override string ToString() => $"Number ({OutputType.Name})";
    }
}
