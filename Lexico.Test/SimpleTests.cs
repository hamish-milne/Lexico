using Xunit;

namespace Lexico.Test
{
    public class SimpleTests
    {
        class LiteralTestObj
        {
            [Literal("foo")] Unnamed _;
            [Literal("bar")] Unnamed __;
        }

        [Fact]
        public void LiteralTest()
        {
            Assert.True(Lexico.TryParse<LiteralTestObj>("foobar", out var outObj));
            Assert.NotNull(outObj);
            Assert.False(Lexico.TryParse<LiteralTestObj>("fooba", out var _));
        }
    }
}