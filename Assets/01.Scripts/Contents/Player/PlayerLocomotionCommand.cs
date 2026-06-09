using UnityEngine;

public enum PlayerLocomotionCommandType
{
    Move = 0,
    Jump = 1,
    Dash = 2
}

public struct PlayerLocomotionCommand
{
    public PlayerLocomotionCommandType Type;
    public InputCommandPhase Phase;
    public Vector2 MoveVector;
    public Vector2 DashVector;

    public bool IsStarted => Phase == InputCommandPhase.Started || Phase == InputCommandPhase.Performed;
    public bool IsCanceled => Phase == InputCommandPhase.Canceled;

    public static PlayerLocomotionCommand CreateMove(Vector2 direction)
    {
        return new PlayerLocomotionCommand
        {
            Type = PlayerLocomotionCommandType.Move,
            Phase = direction.sqrMagnitude > 0.0001f ? InputCommandPhase.Performed : InputCommandPhase.Canceled,
            MoveVector = direction
        };
    }

    public static PlayerLocomotionCommand CreateJump(bool started)
    {
        return new PlayerLocomotionCommand
        {
            Type = PlayerLocomotionCommandType.Jump,
            Phase = started ? InputCommandPhase.Started : InputCommandPhase.Canceled
        };
    }

    public static PlayerLocomotionCommand CreateDash(Vector2 direction)
    {
        return new PlayerLocomotionCommand
        {
            Type = PlayerLocomotionCommandType.Dash,
            Phase = InputCommandPhase.Started,
            DashVector = direction
        };
    }
}
