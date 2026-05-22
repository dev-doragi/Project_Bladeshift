using UnityEngine;

/// <summary>
/// [BladeShift PoC] 상태/입력/씬/전투 흐름에서 사용하는 공통 이벤트 정의입니다.
/// </summary>

#region [1. Core State Events]
public struct GameStateChangedEvent
{
    public GameState PreviousState;
    public GameState NewState;
}

public struct InGameStateChangedEvent
{
    public InGameState NewState;
}

public struct PauseRequestedEvent
{
    public bool Pause;
}

public struct SlowMotionEvent
{
    public float TargetTimeScale;
    public float Duration;
}
#endregion

#region [2. Scene Events]
public struct SceneLoadRequestedEvent
{
    public string SceneName;
    public bool HasPostLoadState;
    public GameState PostLoadState;
}

public struct SceneLoadedEvent
{
    public string SceneName;
}
#endregion

#region [3. Stage & Wave Flow Events]
public struct StageLoadedEvent
{
    public int StageIndex;
}

public struct StageClearedEvent
{
    public int StageIndex;
    public bool IsFinalStage;
}

public struct StageFailedEvent
{
    public int StageIndex;
}

public struct WaveStartedEvent
{
    public int StageIndex;
    public int WaveIndex;
}

public struct WaveEndedEvent
{
    public int StageIndex;
    public int WaveIndex;
    public bool IsWin;
    public bool IsFinalWave;
}

public struct WaveWaitInterruptedEvent { }

public struct WaveWaitTimerTickEvent
{
    public float RemainingTime;
}
#endregion

#region [4. Input Events]
public struct MoveInputEvent
{
    public Vector2 Direction;
}

public struct JumpInputEvent
{
    public bool IsStarted;
}

public struct PrimaryAttackEvent
{
    public bool IsStarted;
}

public struct SecondaryAttackEvent
{
    public bool IsStarted;
}

public struct RotateEvent { }

public struct ScrollEvent
{
    public float Delta;
}

public struct PausePressedEvent { }
#endregion

#region [5. Combat & Weapon Events]
public struct WeaponStateChangeEvent
{
    public WeaponState NewState;
}

public struct HitStopEvent
{
    public float Duration;
}

public struct CameraShakeEvent
{
    public ShakeIntensity Intensity;
}
#endregion

#region [6. Audio & Camera Events]
public struct PlaySFXEvent
{
    public AudioClip Clip;
    public float Volume;
}

public struct CameraManipulationEvent { }
#endregion
