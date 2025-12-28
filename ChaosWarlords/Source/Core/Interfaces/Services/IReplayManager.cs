namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    public interface IReplayManager
    {
        bool IsReplaying { get; }
        void StartReplay(string replayJson);
        string GetRecordingJson();
        void StopReplay();
    }
}
