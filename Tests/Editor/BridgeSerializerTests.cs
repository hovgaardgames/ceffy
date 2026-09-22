using System.Reflection;
using Ceffy.Bridge;
using NUnit.Framework;

namespace Ceffy.Tests.Editor
{
    public class BridgeSerializerTests
    {
        [Test]
        public void SerializeRequest_PreservesRpcContract()
        {
            var payload = new UserPayload
            {
                DisplayName = "Alice",
                OptionalValue = null
            };

            var json = BridgeSerializer.SerializeRequest("saveUser", new object[] { payload }, 7);

            Assert.AreEqual(
                "{\"method\":\"saveUser\",\"params\":[{\"displayName\":\"Alice\"}],\"id\":7}",
                json);
        }

        [Test]
        public void SerializeResponse_PreservesComplexResultContract()
        {
            var payload = new UserPayload
            {
                DisplayName = "Alice"
            };

            var json = BridgeSerializer.SerializeResponse(3, payload);

            Assert.AreEqual("{\"id\":3,\"result\":{\"displayName\":\"Alice\"}}", json);
        }

        [Test]
        public void ConvertParameters_ConvertsJsonValuesToMethodTypes()
        {
            var method = typeof(BridgeSerializerTests).GetMethod(
                nameof(AcceptPayload),
                BindingFlags.NonPublic | BindingFlags.Static);
            var parameters = BridgeSerializer.ExtractParams(
                "{\"params\":[{\"displayName\":\"Alice\"},3]}");

            var converted = BridgeSerializer.ConvertParameters(method, parameters);

            var payload = (UserPayload)converted[0];
            Assert.AreEqual("Alice", payload.DisplayName);
            Assert.AreEqual(3, converted[1]);
        }

        [Test]
        public void ConvertParameters_UsesOptionalDefaultsWhenParametersAreMissing()
        {
            var method = typeof(BridgeSerializerTests).GetMethod(
                nameof(AcceptOptional),
                BindingFlags.NonPublic | BindingFlags.Static);

            var converted = BridgeSerializer.ConvertParameters(method, null);

            Assert.AreEqual(4, converted[0]);
            Assert.AreEqual("ready", converted[1]);
        }

        private static void AcceptPayload(UserPayload payload, int count)
        {
        }

        private static void AcceptOptional(int count = 4, string label = "ready")
        {
        }

        private class UserPayload
        {
            public string DisplayName { get; set; }
            public string OptionalValue { get; set; }
        }
    }
}
