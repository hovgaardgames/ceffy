using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ceffy.Bridge;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class CeffyBridgeTests
    {
        private FakeBridgeTransport _transport;
        private CeffyBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeBridgeTransport();
            _bridge = new CeffyBridge(_transport);
        }

        [TearDown]
        public void TearDown()
        {
            if (_transport.SubscriberCount > 0)
                _bridge.Dispose();
        }

        [Test]
        public void CreateProxy_VoidCallSendsNotification()
        {
            var proxy = _bridge.CreateProxy<ITestUiMethods>();

            proxy.ShowMessage("ready");

            Assert.AreEqual(1, _transport.SentMessages.Count);
            Assert.AreEqual("{\"method\":\"showMessage\",\"params\":[\"ready\"]}", _transport.SentMessages[0]);
        }

        [Test]
        public void CreateProxy_ResponseCompletesTask()
        {
            var proxy = _bridge.CreateProxy<ITestUiMethods>();

            var task = proxy.GetCount("items");
            _transport.ReceiveMessage("{\"id\":1,\"result\":3}");

            Assert.AreEqual("{\"method\":\"getCount\",\"params\":[\"items\"],\"id\":1}", _transport.SentMessages[0]);
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
            Assert.AreEqual(3, task.Result);
        }

        [Test]
        public void CreateProxy_EscapedErrorFaultsTask()
        {
            const string error = "Line 1\n\"quoted\"\\path Æ";
            var proxy = _bridge.CreateProxy<ITestUiMethods>();

            var task = proxy.GetCount("items");
            _transport.ReceiveMessage(BridgeSerializer.SerializeError(1, error));

            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual(error, task.Exception.InnerException.Message);
        }

        [Test]
        public void InjectBridgeRuntime_FlushesQueuedScriptsInOrder()
        {
            _bridge.Bind<ITestUnityMethods>(new TestUnityMethods());
            _bridge.CreateProxy<ITestUiMethods>();
            Assert.AreEqual(0, _transport.ExecutedScripts.Count);

            _bridge.InjectBridgeRuntime();

            Assert.AreEqual(3, _transport.ExecutedScripts.Count);
            StringAssert.Contains("_bridgeReady", _transport.ExecutedScripts[0]);
            StringAssert.Contains("window.ceffy.unity.add", _transport.ExecutedScripts[1]);
            StringAssert.Contains("window.ceffy.ui.showMessage", _transport.ExecutedScripts[2]);
        }

        [Test]
        public void Dispose_StopsCommunication()
        {
            var proxy = _bridge.CreateProxy<ITestUiMethods>();
            Assert.AreEqual(1, _transport.SubscriberCount);

            _bridge.Dispose();
            proxy.ShowMessage("ignored");

            Assert.AreEqual(0, _transport.SubscriberCount);
            Assert.AreEqual(0, _transport.SentMessages.Count);
        }

        public interface ITestUiMethods
        {
            void ShowMessage(string message);
            Task<int> GetCount(string key);
        }

        public interface ITestUnityMethods
        {
            int Add(int left, int right);
        }

        private class TestUnityMethods : ITestUnityMethods
        {
            public int Add(int left, int right)
            {
                return left + right;
            }
        }

        private class FakeBridgeTransport : ICeffyBridgeTransport
        {
            private Action<string> _messageReceived;

            public List<string> SentMessages { get; } = new List<string>();
            public List<string> ExecutedScripts { get; } = new List<string>();
            public int SubscriberCount { get; private set; }

            public event Action<string> MessageReceived
            {
                add
                {
                    _messageReceived += value;
                    SubscriberCount++;
                }
                remove
                {
                    _messageReceived -= value;
                    SubscriberCount--;
                }
            }

            public void ExecuteJavaScript(string javaScript)
            {
                ExecutedScripts.Add(javaScript);
            }

            public void SendMessage(string message)
            {
                SentMessages.Add(message);
            }

            public void ReceiveMessage(string message)
            {
                _messageReceived?.Invoke(message);
            }
        }
    }
}
