using Xunit.Abstractions;

namespace Lexico.Test
{
    public sealed class XunitTrace : DeveloperTrace
    {
        private readonly ITestOutputHelper _outputHelper;

        public XunitTrace(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

        protected override void WriteLine(bool isPop, bool success, string str) => _outputHelper.WriteLine(str);
    }
}