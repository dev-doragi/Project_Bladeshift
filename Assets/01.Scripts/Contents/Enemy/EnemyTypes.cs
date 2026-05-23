public enum EnemyCategory
{
    Normal = 0,
    Heavy = 1,
    Elite = 2,
    Boss = 3
}

public enum GroggyRightClickActionType
{
    None = 0,
    Capture = 1,
    EmbeddedAttack = 2
}

public enum GroggyTriggerMode
{
    None = 0,
    HealthThreshold = 1,
    Gauge = 2
}

public enum PiercingAttackPolicy
{
    StickToEnemy = 0,
    PassThroughToWall = 1,
    StickOnlyWhileGroggy = 2
}

public enum PierceContactResult
{
    WallPinned = 0,
    EnemyCaptured = 1,
    PassThrough = 2
}
