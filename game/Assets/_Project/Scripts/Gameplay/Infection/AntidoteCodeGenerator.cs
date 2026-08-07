using System.Text;

namespace MonkeyLab.Gameplay.Infection
{
    /// <summary>
    /// 백신실 중앙 제어 PC가 발급하는 배합 코드를 생성한다(GDD §14.2, SDD §12.1).
    /// 코드는 서버에서만 생성하고 요청한 클라이언트에게만 전송한다.
    /// </summary>
    public static class AntidoteCodeGenerator
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>
        /// 서버에서만 호출한다. 같은 seed면 항상 같은 코드를 준다(EditMode 테스트용).
        /// </summary>
        public static string Generate(int length, int seed)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            var random = (uint)(seed == 0 ? 1 : seed);
            var builder = new StringBuilder(length);
            for (var index = 0; index < length; index++)
            {
                random = NextRandom(random);
                builder.Append(Alphabet[(int)(random % (uint)Alphabet.Length)]);
            }

            return builder.ToString();
        }

        private static uint NextRandom(uint state)
        {
            // Xorshift32. Unity 난수를 쓰지 않아 서버·테스트 결과가 일치한다.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
