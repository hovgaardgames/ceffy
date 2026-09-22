using Ceffy.Bridge;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class BridgeNamingTests
    {
        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase("alreadyCamel", "alreadyCamel")]
        [TestCase("OpenWindow", "openWindow")]
        [TestCase("URLValue", "urlValue")]
        [TestCase("HTML", "html")]
        public void ToCamelCase_ProducesJavaScriptMethodName(string value, string expected)
        {
            Assert.AreEqual(expected, BridgeNaming.ToCamelCase(value));
        }
    }
}
