using System;
using System.Text;
using MonkeyLab.Network;
using NUnit.Framework;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class ConnectionIdentityPayloadTests
    {
        [Test]
        public void PlayerId_RoundTripsWithoutAuthenticationToken()
        {
            const string playerId = "unity-player-123456789";

            Assert.That(
                ConnectionIdentityPayload.TryEncode(playerId, out var payload),
                Is.True);
            Assert.That(
                ConnectionIdentityPayload.TryDecode(payload, out var decoded),
                Is.True);
            Assert.That(decoded, Is.EqualTo(playerId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(" player-id")]
        [TestCase("player-id\n")]
        public void InvalidPlayerId_IsRejected(string playerId)
        {
            Assert.That(
                ConnectionIdentityPayload.TryEncode(playerId, out _),
                Is.False);
        }

        [Test]
        public void OversizedPayload_IsRejected()
        {
            var payload = Encoding.UTF8.GetBytes(
                new string('x',
                    ConnectionIdentityPayload.MaximumPayloadBytes + 1));

            Assert.That(
                ConnectionIdentityPayload.TryDecode(payload, out _),
                Is.False);
        }

        [Test]
        public void InvalidUtf8_IsRejected()
        {
            var payload = new byte[] { 0xC3, 0x28 };

            Assert.That(
                ConnectionIdentityPayload.TryDecode(payload, out _),
                Is.False);
        }
    }
}
