using Ceffy.Bridge;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class BridgeJavaScriptTests
    {
        [Test]
        public void GenerateUnityMethodStub_UsesUnityRpcContract()
        {
            var generated = BridgeJavaScript.GenerateUnityMethodStub("OpenPanel");

            StringAssert.Contains("window.ceffy.unity.openPanel", generated);
            StringAssert.Contains("window.ceffy._rpc('openPanel'", generated);
        }

        [Test]
        public void GenerateUiMethodPlaceholder_UsesUiContract()
        {
            var generated = BridgeJavaScript.GenerateUiMethodPlaceholder("ShowPanel");

            Assert.AreEqual(
                "window.ceffy.ui.showPanel = window.ceffy.ui.showPanel || null;",
                generated);
        }
    }
}
