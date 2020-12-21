using System;
using Xunit;

namespace Lexico.Test
{
    public class TraceTests
    {
        [Fact]
        public void EmptyUserTraceDoesntBlowUp()
        {
            var result = Lexico.TryParse("5", out int i, new UserTrace(Console.WriteLine));
        }
    }
}