using Xunit;

namespace Lexico.Test
{
    public class UserObjectTests
    {
        public class ClassWithUserObject
        {
            [Term] public int Number;
            [UserObject] public object UserObject;
        }
        
        [Fact]
        public void CanRetrieveUserObject()
        {
            var myUserObject = new object();
            var obj = Lexico.Parse<ClassWithUserObject>("5", userObject: myUserObject);
            Assert.Equal(myUserObject, obj.UserObject);
        }
        
        [Fact]
        public void CanRetrieveUserObjectValueType()
        {
            var myUserObject = 3.14f;
            var obj = Lexico.Parse<ClassWithUserObject>("5", userObject: myUserObject);
            Assert.Equal(myUserObject, obj.UserObject);
        }
    }
}