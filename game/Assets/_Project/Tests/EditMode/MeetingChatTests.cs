using MonkeyLab.Gameplay.Application;
using MonkeyLab.Gameplay.Infection;
using MonkeyLab.Gameplay.Meeting;
using NUnit.Framework;
using UnityEngine;

namespace MonkeyLab.Tests.EditMode
{
    /// <summary>
    /// 토론 채팅 규칙을 검증한다.
    /// 기준: GDD §16.2, §17, docs/system-design-document.md §11.5,
    /// docs/ui-ux-design.md §11.1, docs/qa-and-playtest-plan.md §280.
    /// </summary>
    public sealed class MeetingChatTests
    {
        private const int MaximumLength = 80;

        private RoundBalanceConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RoundBalanceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        // --- 밸런스 표 동기화 (balance-and-telemetry.md §2) ---

        [Test]
        public void ChatBalance_MatchesBalanceTable()
        {
            Assert.That(_config.ChatMessageMaximumLength, Is.EqualTo(80));
            Assert.That(
                _config.ChatMessageIntervalSeconds,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(_config.ChatHistoryMaximumCount, Is.EqualTo(60));
        }

        // --- 문자열 정리 (QA §280 Rich Text 차단) ---

        [Test]
        public void Sanitize_RemovesRichTextTags()
        {
            var sanitized = MeetingChatRules.Sanitize(
                "<color=red><b>빨강</b></color> 이 사람 수상함",
                MaximumLength);

            Assert.That(sanitized, Does.Not.Contain("<"));
            Assert.That(sanitized, Does.Not.Contain(">"));
            Assert.That(sanitized, Does.Contain("빨강"));
            Assert.That(sanitized, Does.Contain("이 사람 수상함"));
        }

        [Test]
        public void Sanitize_CollapsesNewlinesAndRepeatedSpaces()
        {
            var sanitized = MeetingChatRules.Sanitize(
                "실험실 A\n\n\n붉은 연기\t\t봤음     진짜로",
                MaximumLength);

            Assert.That(sanitized, Does.Not.Contain("\n"));
            Assert.That(sanitized, Does.Not.Contain("\t"));
            Assert.That(sanitized, Does.Not.Contain("  "));
            Assert.That(
                sanitized,
                Is.EqualTo("실험실 A 붉은 연기 봤음 진짜로"));
        }

        [Test]
        public void Sanitize_TruncatesToMaximumLength()
        {
            var sanitized = MeetingChatRules.Sanitize(
                new string('가', 200),
                MaximumLength);

            Assert.That(sanitized.Length, Is.EqualTo(MaximumLength));
        }

        [Test]
        public void Sanitize_NeverExceedsMaximumLengthWithFoldedSpaces()
        {
            // 보류된 공백을 붙일 때 한 글자를 넘기지 않아야 한다.
            var sanitized = MeetingChatRules.Sanitize("abcd ef", 5);

            Assert.That(sanitized.Length, Is.LessThanOrEqualTo(5));
            Assert.That(sanitized, Is.EqualTo("abcd"));
        }

        [Test]
        public void Sanitize_ReturnsEmptyForBlankInput()
        {
            Assert.That(
                MeetingChatRules.Sanitize(null, MaximumLength),
                Is.Empty);
            Assert.That(
                MeetingChatRules.Sanitize("   \n\t  ", MaximumLength),
                Is.Empty);
            Assert.That(
                MeetingChatRules.Sanitize("<<<>>>", MaximumLength),
                Is.Empty,
                "태그만 있는 메시지는 정리 후 빈 문자열이어야 한다.");
        }

        [Test]
        public void Sanitize_DoesNotLeadWithSpace()
        {
            Assert.That(
                MeetingChatRules.Sanitize("    앞 공백", MaximumLength),
                Is.EqualTo("앞 공백"));
        }

        // --- 전송 검증 ---

        [Test]
        public void Validate_AllowsAliveParticipantDuringDiscussion()
        {
            Assert.That(
                Validate(),
                Is.EqualTo(ChatRejectionReason.None));
        }

        [Test]
        public void Validate_AllowsInfectedSurvivor()
        {
            Assert.That(
                Validate(lifeState: PlayerLifeState.AliveInfected),
                Is.EqualTo(ChatRejectionReason.None),
                "감염 중에도 회의에 참여한다.");
        }

        [Test]
        public void Validate_RejectsOutsideDiscussionPhase()
        {
            Assert.That(
                Validate(isDiscussionPhase: false),
                Is.EqualTo(ChatRejectionReason.NotDiscussionPhase),
                "탐색 중 일반 채팅은 MVP 범위가 아니다(GDD §16.2).");
        }

        [Test]
        public void Validate_RejectsGhost()
        {
            Assert.That(
                Validate(lifeState: PlayerLifeState.DeadGhost),
                Is.EqualTo(ChatRejectionReason.NotAlive),
                "유령은 살아 있는 플레이어와 대화할 수 없다(GDD §17).");
        }

        [Test]
        public void Validate_RejectsUnregisteredParticipant()
        {
            Assert.That(
                Validate(isRegisteredParticipant: false),
                Is.EqualTo(ChatRejectionReason.NotParticipant));
        }

        [Test]
        public void Validate_RejectsEmptyMessage()
        {
            Assert.That(
                Validate(sanitizedMessage: string.Empty),
                Is.EqualTo(ChatRejectionReason.EmptyMessage));
        }

        [Test]
        public void Validate_RejectsMessagesFasterThanInterval()
        {
            Assert.That(
                Validate(serverTime: 100.9d, lastSentServerTime: 100d),
                Is.EqualTo(ChatRejectionReason.TooFrequent));
        }

        [Test]
        public void Validate_AllowsMessageAtExactlyInterval()
        {
            Assert.That(
                Validate(serverTime: 101d, lastSentServerTime: 100d),
                Is.EqualTo(ChatRejectionReason.None));
        }

        [Test]
        public void Validate_AllowsFirstMessageWithoutIntervalCheck()
        {
            Assert.That(
                Validate(serverTime: 0.2d, lastSentServerTime: 0d),
                Is.EqualTo(ChatRejectionReason.None),
                "첫 발언은 간격 검사를 받지 않아야 한다.");
        }

        [Test]
        public void Validate_ChecksPhaseBeforeLifeState()
        {
            // 유령이 탐색 중에 보내면 단계 거부가 먼저 나와야 한다.
            Assert.That(
                Validate(
                    isDiscussionPhase: false,
                    lifeState: PlayerLifeState.DeadGhost),
                Is.EqualTo(ChatRejectionReason.NotDiscussionPhase));
        }

        private static ChatRejectionReason Validate(
            bool isDiscussionPhase = true,
            PlayerLifeState lifeState = PlayerLifeState.AliveHealthy,
            bool isRegisteredParticipant = true,
            string sanitizedMessage = "실험실 A에 붉은 연기 봄",
            double serverTime = 500d,
            double lastSentServerTime = 100d,
            float minimumIntervalSeconds = 1f)
        {
            return MeetingChatRules.Validate(
                isDiscussionPhase,
                lifeState,
                isRegisteredParticipant,
                sanitizedMessage,
                serverTime,
                lastSentServerTime,
                minimumIntervalSeconds);
        }
    }
}
