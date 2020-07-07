using System;
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

        public class ClassWithConcreteUserObject
        {
            [Term] public int Number;
            [UserObject] public float UserObject;
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

        [Fact]
        public void CanUseConcreteTypeForUserObject()
        {
            var myUserObject = 3.14f;
            var obj = Lexico.Parse<ClassWithConcreteUserObject>("5", userObject: myUserObject);
            Assert.Equal(myUserObject, obj.UserObject);
        }
        
        [Fact]
        public void WrongUserObjectTypeThrowsCastException()
        {
            var myUserObject = 3;
            Assert.Throws<InvalidCastException>(() => Lexico.Parse<ClassWithConcreteUserObject>("5", userObject: myUserObject));
        }
    }
}