[System.Serializable]
public sealed class EnemyGroggyState
{
    public bool IsGroggy;
    public bool IsGroggyInvulnerable;
    public bool IsRecoveringFromGroggy;
    public bool IsCaptured;
    public bool IsPierced;
    public float CurrentGroggyGauge;

    public void ResetAll()
    {
        IsGroggy = false;
        IsGroggyInvulnerable = false;
        IsRecoveringFromGroggy = false;
        IsCaptured = false;
        IsPierced = false;
        CurrentGroggyGauge = 0f;
    }
}
