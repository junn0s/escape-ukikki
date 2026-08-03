using System;
using System.Text;

namespace MonkeyLab.Network
{
    /// <summary>
    /// Unity Authentication PlayerId를 NGO 연결 승인 데이터로 변환한다.
    /// 재접속 때 새 clientId가 발급되어도 같은 참가자인지 찾는 서버 키다.
    /// 인증 토큰은 절대 포함하지 않는다(technical-design-document.md §3).
    /// </summary>
    public static class ConnectionIdentityPayload
    {
        public const int MaximumPayloadBytes = 128;

        private static readonly UTF8Encoding StrictUtf8 =
            new(encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

        public static bool TryEncode(string playerId, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (!IsValidPlayerId(playerId))
            {
                return false;
            }

            var encoded = StrictUtf8.GetBytes(playerId);
            if (encoded.Length == 0 || encoded.Length > MaximumPayloadBytes)
            {
                return false;
            }

            payload = encoded;
            return true;
        }

        public static bool TryDecode(byte[] payload, out string playerId)
        {
            playerId = string.Empty;
            if (payload == null || payload.Length == 0 ||
                payload.Length > MaximumPayloadBytes)
            {
                return false;
            }

            try
            {
                var decoded = StrictUtf8.GetString(payload);
                if (!IsValidPlayerId(decoded))
                {
                    return false;
                }

                playerId = decoded;
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static bool IsValidPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId) ||
                !string.Equals(playerId, playerId.Trim(),
                    StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 0; index < playerId.Length; index++)
            {
                if (char.IsControl(playerId[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
