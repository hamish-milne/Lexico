using System;

namespace Lexico
{
    [Flags]
    public enum ParserFlags
    {
        None = 0,
        
        /// <summary>
        /// Do not push or pop this Parser to ITrace implementations. 
        /// </summary>
        IgnoreInTrace = 1 << 0,
        
        /// <summary>
        /// Treat this parser as a header in ITrace.
        /// This flag is a hint, the effects depend on the ITrace implementations.
        /// </summary>
        TraceHeader = 1 << 1
    }

    public class ParserFlagsAttribute : ConfigAttribute, IConfig<ParserFlags>
    {
        private readonly ParserFlags _flags;

        public ParserFlagsAttribute(ParserFlags flags) => _flags = flags;

        public void ApplyConfig(ref ParserFlags value) => value |= _flags;
    }
    
    /// <summary>
    /// A stateless object that consumes text and turns it into values
    /// </summary>
    public interface IParser
    {
        Type OutputType { get; }
        void Compile(ICompileContext context);
        ParserFlags Flags { get; }
    }

    public abstract class ParserBase : IParser
    {
        protected ParserBase(IConfig config, ParserFlags flags) => Flags = flags | config.Get(ParserFlags.None);
        public abstract Type OutputType { get; }
        public abstract void Compile(ICompileContext context);
        public ParserFlags Flags { get; }
    }
}