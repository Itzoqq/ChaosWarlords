using ChaosWarlords.Source.Core.Interfaces.State;

using ChaosWarlords.Source.States;

public interface IGameCommand
{
    void Execute(IGameplayState state);
}



