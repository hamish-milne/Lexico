using System;
using System.Text.RegularExpressions;
using System.Globalization;
using static System.AttributeTargets;

namespace Lexico
{
    /// <summary>
    /// Provides configuration parameters to the parser generators
    /// </summary>
    public interface IConfig
    {
        /// <summary>
        /// Gets the config value of the given Type (T)
        /// </summary>
        /// <param name="defaultValue">The default value to return if there is nothing set</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>(T defaultValue);
    }

    public class DefaultConfig : IConfig
    {
        private DefaultConfig() {}
        public static DefaultConfig Instance { get; } = new DefaultConfig();
        public T Get<T>(T defaultValue) => defaultValue;
    }

    /// <summary>
    /// Base interface of IConfig`1 used as a marker
    /// </summary>
    public interface IConfigBase {}

    /// <summary>
    /// Implemented by ConfigAttributes to provide configuration values
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IConfig<T> : IConfigBase
    {
        /// <summary>
        /// Modifies `value`, applying this object's configuration for `T`
        /// </summary>
        /// <param name="value"></param>
        void ApplyConfig(ref T value);
    }

    /// <summary>
    /// Base class for configuration attributes
    /// </summary>
    [AttributeUsage(Field | Property | Class | Struct, AllowMultiple = true)]
    public abstract class ConfigAttribute : Attribute {}

    /// <summary>
    /// Sets the NumberStyles to use for this member and its direct children
    /// </summary>
    public class NumberStyleAttribute : ConfigAttribute, IConfig<NumberStyles>
    {
        public NumberStyleAttribute(NumberStyles styles) => value = styles;

        private readonly NumberStyles value;

        public void ApplyConfig(ref NumberStyles value) => value = this.value;
    }

    /// <summary>
    /// Sets the RegexOptions to use for this member and its direct children
    /// </summary>
    public class RegexOptionsAttribute : ConfigAttribute, IConfig<RegexOptions>
    {
        public RegexOptionsAttribute(RegexOptions options) => value = options;

        private readonly RegexOptions value;

        public void ApplyConfig(ref RegexOptions value) => value = this.value;
    }

    /// <summary>
    /// Indicates that all whitespace can include newlines for this member and its direct children
    /// </summary>
    public class MultiLineAttribute : ConfigAttribute, IConfig<RegexOptions>
    {
        public void ApplyConfig(ref RegexOptions value) => value |= RegexOptions.Multiline;
    }
}