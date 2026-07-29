using System.Threading.Tasks;

namespace MonkeyLab.Core
{
    public interface IBootstrapTask
    {
        bool IsReady { get; }
        string FailureMessage { get; }
        Task InitializeAsync();
    }
}
