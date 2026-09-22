using System.Collections.Generic;

namespace Ceffy.Bridge
{
    internal class BridgeRequest
    {
        public string method;
        public List<object> @params;
        public int? id;
    }

    internal class BridgeResponse
    {
        public int id;
        public object result;
    }

    internal class BridgeError
    {
        public int id;
        public string error;
    }
}
