using System;

namespace Ceffy.Bridge
{
    internal interface ICeffyBridgeTransport
    {
        event Action<string> MessageReceived;

        void ExecuteJavaScript(string javaScript);
        void SendMessage(string message);
    }
}
