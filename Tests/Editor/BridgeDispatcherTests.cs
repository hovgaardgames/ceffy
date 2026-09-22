using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ceffy.Bridge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ceffy.Tests.Editor
{
    public class BridgeDispatcherTests
    {
        [Test]
        public void ProcessMessages_InvokesBoundMethodAndReturnsResponse()
        {
            var responses = new List<string>();
            var dispatcher = new BridgeDispatcher(responses.Add);
            var calculator = new CalculatorMethods();
            dispatcher.Bind<ICalculatorMethods>(calculator);

            dispatcher.Enqueue("{\"method\":\"add\",\"params\":[2,3],\"id\":9}");
            dispatcher.ProcessMessages();

            Assert.AreEqual(5, calculator.LastResult);
            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("{\"id\":9,\"result\":5}", responses[0]);
        }

        [Test]
        public void ProcessMessages_NotificationInvokesMethodWithoutResponse()
        {
            var responses = new List<string>();
            var dispatcher = new BridgeDispatcher(responses.Add);
            var calculator = new CalculatorMethods();
            dispatcher.Bind<ICalculatorMethods>(calculator);

            dispatcher.Enqueue("{\"method\":\"notify\",\"params\":[\"ready\"]}");
            dispatcher.ProcessMessages();

            Assert.AreEqual("ready", calculator.LastNotification);
            Assert.AreEqual(0, responses.Count);
        }

        [Test]
        public void ProcessMessages_UnknownMethodReturnsError()
        {
            var responses = new List<string>();
            var dispatcher = new BridgeDispatcher(responses.Add);
            dispatcher.Bind<ICalculatorMethods>(new CalculatorMethods());
            LogAssert.Expect(LogType.Error, "[Ceffy Bridge] Method 'missing' not found in any bound interface.");

            dispatcher.Enqueue("{\"method\":\"missing\",\"id\":10}");
            dispatcher.ProcessMessages();

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("{\"id\":10,\"error\":\"Method 'missing' not found\"}", responses[0]);
        }

        [Test]
        public void ProcessMessages_ThrownMethodReturnsError()
        {
            var responses = new List<string>();
            var dispatcher = new BridgeDispatcher(responses.Add);
            dispatcher.Bind<ICalculatorMethods>(new CalculatorMethods());
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: calculation failed"));

            dispatcher.Enqueue("{\"method\":\"fail\",\"id\":11}");
            dispatcher.ProcessMessages();

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("{\"id\":11,\"error\":\"calculation failed\"}", responses[0]);
        }

        [UnityTest]
        public IEnumerator ProcessMessages_AsyncMethodReturnsResultAfterCompletion()
        {
            var responses = new List<string>();
            var dispatcher = new BridgeDispatcher(responses.Add);
            var calculator = new CalculatorMethods();
            dispatcher.Bind<ICalculatorMethods>(calculator);

            dispatcher.Enqueue("{\"method\":\"multiplyAsync\",\"params\":[4,5],\"id\":12}");
            dispatcher.ProcessMessages();

            Assert.AreEqual(0, responses.Count);
            calculator.CompleteMultiplication();
            yield return null;

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("{\"id\":12,\"result\":20}", responses[0]);
        }

        private interface ICalculatorMethods
        {
            int Add(int left, int right);
            void Notify(string value);
            int Fail();
            Task<int> MultiplyAsync(int left, int right);
        }

        private class CalculatorMethods : ICalculatorMethods
        {
            private readonly TaskCompletionSource<int> multiplication = new TaskCompletionSource<int>();
            private int product;

            public int LastResult { get; private set; }
            public string LastNotification { get; private set; }

            public int Add(int left, int right)
            {
                LastResult = left + right;
                return LastResult;
            }

            public void Notify(string value)
            {
                LastNotification = value;
            }

            public int Fail()
            {
                throw new InvalidOperationException("calculation failed");
            }

            public Task<int> MultiplyAsync(int left, int right)
            {
                product = left * right;
                return multiplication.Task;
            }

            public void CompleteMultiplication()
            {
                multiplication.SetResult(product);
            }
        }
    }
}
