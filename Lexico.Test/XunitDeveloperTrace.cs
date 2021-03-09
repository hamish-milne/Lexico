using Xunit.Abstractions;

namespace Lexico.Test
{
    internal sealed class XunitDeveloperTrace : DeveloperTrace
    {
        private readonly ITestOutputHelper _outputHelper;

        public XunitDeveloperTrace(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

        protected override void WriteLine(bool isPop, bool success, string str) => _outputHelper.WriteLine(str);
    }

    internal sealed class XunitUserTrace : UserTrace
    {
        public XunitUserTrace(ITestOutputHelper outputHelper) : base(outputHelper.WriteLine) { }
    }
}