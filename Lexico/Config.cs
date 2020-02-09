

using System;
using System.Text.RegularExpressions;
using System.Globalization;
using static System.AttributeTargets;

namespace Lexico
{
    public interface IConfig
    {
        T Get<T>(T defaultValue);
    }

    public interface IConfig<T>
    {
        void ApplyConfig(ref T value);
    }

    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = true)]
    public abstract class ConfigAttribute : Attribute {}

    public class NumberStyleAttribute : ConfigAttribute, IConfig<NumberStyles>
    {
        public NumberStyleAttribute(NumberStyles styles) => value = styles;

        private readonly NumberStyles value;

        public void ApplyConfig(ref NumberStyles value) => value = this.value;
    }

    public class RegexOptionsAttribute : ConfigAttribute, IConfig<RegexOptions>
    {
        public RegexOptionsAttribute(RegexOptions options) => value = options;

        private readonly RegexOptions value;

        public void ApplyConfig(ref RegexOptions value) => value = this.value;
    }

    public class MultiLineAttribute : ConfigAttribute, IConfig<RegexOptions>
    {
        public void ApplyConfig(ref RegexOptions value) => value |= RegexOptions.Multiline;
    }
}