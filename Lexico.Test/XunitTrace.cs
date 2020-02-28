using Xunit.Abstractions;

namespace Lexico.Test
{
    internal sealed class XunitTrace : TextTrace
    {
        private readonly ITestOutputHelper _outputHelper;

        public XunitTrace(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

        protected override void WriteLine(bool isPop, bool success, string str) => _outputHelper.WriteLine(str);
    }
}