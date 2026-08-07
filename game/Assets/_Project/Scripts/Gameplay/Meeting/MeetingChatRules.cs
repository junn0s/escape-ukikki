using System.Text;
using MonkeyLab.Gameplay.Infection;

namespace MonkeyLab.Gameplay.Meeting
{
    /// <summary>
    /// 토론 채팅의 서버 검증과 문자열 정리 규칙이다.
    /// 기준: GDD §16.2(토론 90초), §17(유령은 살아 있는 플레이어와 대화 불가),
    /// docs/ui-ux-design.md §11.1(최대 글자 수와 초당 메시지 수 제한),
    /// docs/qa-and-playtest-plan.md §280(Rich Text 또는 태그 삽입 차단).
    /// </summary>
    public static class MeetingChatRules
    {
        public static ChatRejectionReason Validate(
            bool isDiscussionPhase,
            PlayerLifeState senderLifeState,
            bool isRegisteredParticipant,
            string sanitizedMessage,
            double serverTime,
            double lastSentServerTime,
            float minimumIntervalSeconds)
        {
            if (!isDiscussionPhase)
            {
                return ChatRejectionReason.NotDiscussionPhase;
            }

            if (senderLifeState == PlayerLifeState.DeadGhost)
            {
                return ChatRejectionReason.NotAlive;
            }

            if (!isRegisteredParticipant)
            {
                return ChatRejectionReason.NotParticipant;
            }

            if (string.IsNullOrEmpty(sanitizedMessage))
            {
                return ChatRejectionReason.EmptyMessage;
            }

            // 첫 전송(lastSentServerTime <= 0)은 간격 검사를 건너뛴다.
            if (lastSentServerTime > 0d &&
                serverTime - lastSentServerTime < minimumIntervalSeconds)
            {
                return ChatRejectionReason.TooFrequent;
            }

            return ChatRejectionReason.None;
        }

        /// <summary>
        /// 서식 태그와 줄바꿈을 제거하고 최대 길이로 자른다.
        /// 각괄호를 지우는 이유는 Unity 텍스트가 Rich Text 태그를 해석해
        /// 색·크기 조작이나 다른 플레이어 사칭이 가능해지기 때문이다.
        /// </summary>
        public static string Sanitize(string message, int maximumLength)
        {
            if (string.IsNullOrEmpty(message) || maximumLength <= 0)
            {
                return string.Empty;
            }

            // macOS·WebGL IME가 정규화 D 형태로 넘긴 한글도 서버에서 NFC로
            // 통일한다. UI는 조합 중 문자열을 보내지 않지만 서버 경계에서도
            // 한 번 더 정리해 플랫폼별 자모 표현 차이를 없앤다.
            var normalized = message.Normalize(NormalizationForm.FormC);
            var builder = new StringBuilder(normalized.Length);
            var hasPendingSpace = false;
            foreach (var character in normalized)
            {
                if (character is '<' or '>')
                {
                    continue;
                }

                if (char.IsControl(character) || character == ' ')
                {
                    // 연속 공백과 줄바꿈을 공백 하나로 접는다.
                    // 선행 공백은 버려서 결과가 공백으로 시작하지 않게 한다.
                    hasPendingSpace = builder.Length > 0;
                    continue;
                }

                // 보류된 공백까지 세서 미리 확인한다. 붙인 뒤에 검사하면
                // 최대 길이를 한 글자 넘길 수 있다.
                var additionalLength = hasPendingSpace ? 2 : 1;
                if (builder.Length + additionalLength > maximumLength)
                {
                    break;
                }

                if (hasPendingSpace)
                {
                    builder.Append(' ');
                    hasPendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
