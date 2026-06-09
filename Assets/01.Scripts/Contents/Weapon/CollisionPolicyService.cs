using UnityEngine;

public static class CollisionPolicyService
{
    public static void SetIgnoreLayerCollision(int firstLayer, int secondLayer, bool ignored)
    {
        if (firstLayer < 0 || firstLayer > 31)
            return;
        if (secondLayer < 0 || secondLayer > 31)
            return;

        Physics2D.IgnoreLayerCollision(firstLayer, secondLayer, ignored);
    }
}
