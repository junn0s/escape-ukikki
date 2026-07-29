using System.Threading.Tasks;

namespace MonkeyLab.Network
{
    public interface IGameSessionGateway
    {
        Task<GameSessionInfo> CreateSessionAsync(GameSessionCreateRequest request);
        Task<GameSessionInfo> JoinSessionByCodeAsync(string joinCode);
        Task LeaveSessionAsync();
    }
}
