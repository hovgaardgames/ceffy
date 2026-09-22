using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Ceffy.Tests.Editor
{
    public class WebBrowserUrlTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://example.com/page")]
        [TestCase("file:///C:/Pages/index.html")]
        public void TransformUrl_LeavesStandardUrlsUnchanged(string url)
        {
            Assert.AreEqual(url, WebBrowser.TransformUrl(url));
        }

        [Test]
        public void TransformUrl_ConvertsStreamingAssetsUrlToFileUrl()
        {
            var fullPath = Path.GetFullPath(
                Path.Combine(Application.streamingAssetsPath, "Pages/My Page.html"));
            var expected = "file:///" + fullPath.Replace("\\", "/").Replace(" ", "%20");

            var transformed = WebBrowser.TransformUrl("streaming-assets://Pages/My Page.html");

            Assert.AreEqual(expected, transformed);
        }
    }
}
