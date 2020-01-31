using System;
using Xunit;

namespace Lexico.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var expr = Parser.Parse<Calculator.Expression>("5+(2+1)");
            Assert.Equal(8.0, expr.Value);
        }
    }
}
