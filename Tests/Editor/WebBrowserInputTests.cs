using NUnit.Framework;
using UnityEngine;

namespace Ceffy.Tests.Editor
{
    public class WebBrowserInputTests
    {
        [TearDown]
        public void TearDown()
        {
            WebBrowserInput.Register(null);
        }

        [Test]
        public void RegisteredProviderReceivesInputQueries()
        {
            var input = new TestWebBrowserInput();
            WebBrowserInput.Register(input);

            Assert.AreEqual(new Vector2(12.0f, 34.0f), WebBrowserInput.GetMousePosition());
            Assert.IsTrue(WebBrowserInput.GetMouseButton(1));
            Assert.IsTrue(WebBrowserInput.GetMouseButtonDown(2));
            Assert.IsTrue(WebBrowserInput.GetMouseButtonUp(0));
            Assert.AreEqual(new Vector2(1.0f, -2.0f), WebBrowserInput.GetMouseScrollDelta());
            Assert.IsTrue(WebBrowserInput.GetShift());
            Assert.IsTrue(WebBrowserInput.GetControl());
            Assert.IsTrue(WebBrowserInput.GetAlt());
            Assert.IsTrue(WebBrowserInput.GetRightAlt());
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, input.QueriedButtons);
        }

        private class TestWebBrowserInput : IWebBrowserInput
        {
            private int _queryIndex;

            public int[] QueriedButtons { get; } = new int[3];

            public Vector2 GetMousePosition()
            {
                return new Vector2(12.0f, 34.0f);
            }

            public bool GetMouseButton(int button)
            {
                QueriedButtons[_queryIndex++] = button;
                return true;
            }

            public bool GetMouseButtonDown(int button)
            {
                QueriedButtons[_queryIndex++] = button;
                return true;
            }

            public bool GetMouseButtonUp(int button)
            {
                QueriedButtons[_queryIndex++] = button;
                return true;
            }

            public Vector2 GetMouseScrollDelta()
            {
                return new Vector2(1.0f, -2.0f);
            }

            public bool GetShift()
            {
                return true;
            }

            public bool GetControl()
            {
                return true;
            }

            public bool GetAlt()
            {
                return true;
            }

            public bool GetRightAlt()
            {
                return true;
            }
        }
    }
}
