using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ceffy.Tests.Runtime
{
    public class WebBrowserRuntimeTests
    {
        private const float TimeoutSeconds = 30.0f;
        private const int TestSize = 64;
        private const string ExpectedMessage = "ceffy-runtime-smoke";
        private const string TestUrl =
            "data:text/html;base64,PGh0bWw+PGJvZHk+Q2VmZnk8c2NyaXB0PndpbmRvdy5jZWZmeS5TZW5kVG9Vbml0eSgnY2VmZnktcn" +
            "VudGltZS1zbW9rZScpOzwvc2NyaXB0PjwvYm9keT48L2h0bWw+";

        private GameObject _browserObject;
        private CeffyInstance _browser;
        private string _receivedMessage;

        [UnityTest]
        public IEnumerator Browser_CompletesNativeSmokeTest()
        {
#if !UNITY_EDITOR_WIN && !UNITY_STANDALONE_WIN
            Assert.Ignore("Ceffy native runtime is currently supported only on Windows.");
            yield break;
#else
            _browserObject = new GameObject("Ceffy Runtime Test Browser");
            _browserObject.SetActive(false);
            _browser = _browserObject.AddComponent<CeffyInstance>();
            _browser.StartUrl = TestUrl;
            _browser.Width = TestSize;
            _browser.Height = TestSize;
            _browser.AutoResizeToRectTransform = false;
            _browser.OnMessageFromCeffy += ReceiveMessage;
            _browserObject.SetActive(true);

            var timeoutAt = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((_browser.Texture == null || _receivedMessage != ExpectedMessage)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.IsNotNull(_browser.Texture, "Browser texture was not created before timeout.");
            Assert.AreEqual(TestSize, _browser.Texture.width);
            Assert.AreEqual(TestSize, _browser.Texture.height);
            Assert.AreEqual(ExpectedMessage, _receivedMessage, "JavaScript message was not received before timeout.");
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_browser != null)
                _browser.OnMessageFromCeffy -= ReceiveMessage;
            if (_browserObject != null)
                Object.Destroy(_browserObject);

            yield return null;

            _browser = null;
            _browserObject = null;
            _receivedMessage = null;
        }

        private void ReceiveMessage(string message)
        {
            _receivedMessage = message;
        }
    }
}
