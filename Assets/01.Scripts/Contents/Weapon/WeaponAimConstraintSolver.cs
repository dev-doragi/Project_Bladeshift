using UnityEngine;

public static class WeaponAimConstraintSolver
{
    private const float WallSkinWidth = 0.02f;
    private const float MinimumMoveDistance = 0.0001f;
    private const int MaxSlideIterations = 3;

    public static Vector2 SolveReachablePosition(
        Vector2 origin,
        Vector2 desiredWorldPosition,
        float controlRadius,
        LayerMask wallMask,
        ref Vector2 lastReachablePosition,
        bool hasLastReachablePosition)
    {
        Vector2 desired = ClampToControlRadius(origin, desiredWorldPosition, controlRadius);
        Vector2 start = origin;
        if (hasLastReachablePosition)
        {
            // Player movement can invalidate the cached position. Recover it before sliding.
            Vector2 previous = ClampToControlRadius(origin, lastReachablePosition, controlRadius);
            start = ClipAtWall(origin, previous, wallMask);
        }

        Vector2 result = MoveAndSlide(start, desired, wallMask);
        result = ClampToControlRadius(origin, result, controlRadius);
        // A clear cursor movement path does not guarantee visibility from the player.
        result = ClipAtWall(origin, result, wallMask);
        lastReachablePosition = result;
        return result;
    }

    private static Vector2 MoveAndSlide(Vector2 start, Vector2 target, LayerMask wallMask)
    {
        Vector2 position = start;
        Vector2 remainingMove = target - start;

        for (int iteration = 0; iteration < MaxSlideIterations; iteration++)
        {
            float distance = remainingMove.magnitude;
            if (distance <= MinimumMoveDistance)
                break;

            Vector2 direction = remainingMove / distance;
            if (!TryGetBlockingHit(position, position + remainingMove, wallMask, out RaycastHit2D hit))
            {
                position += remainingMove;
                break;
            }

            float travelDistance = Mathf.Max(0f, hit.distance - WallSkinWidth);
            Vector2 movementToWall = direction * travelDistance;
            position += movementToWall;
            remainingMove -= movementToWall;

            // Cast the tangential remainder again so sliding cannot skip a second wall.
            float normalMovement = Vector2.Dot(remainingMove, hit.normal);
            if (normalMovement < 0f)
                remainingMove -= hit.normal * normalMovement;
            else
                break;
        }

        return position;
    }

    private static Vector2 ClipAtWall(Vector2 start, Vector2 target, LayerMask wallMask)
    {
        if (!TryGetBlockingHit(start, target, wallMask, out RaycastHit2D hit))
            return target;

        Vector2 direction = (target - start).normalized;
        float travelDistance = Mathf.Max(0f, hit.distance - WallSkinWidth);
        return start + direction * travelDistance;
    }

    private static Vector2 ClampToControlRadius(Vector2 origin, Vector2 position, float controlRadius)
    {
        float radius = Mathf.Max(0f, controlRadius);
        if (radius <= 0f)
            return origin;

        Vector2 offset = position - origin;
        if (offset.sqrMagnitude <= radius * radius)
            return position;

        return origin + offset.normalized * radius;
    }

    private static bool TryGetBlockingHit(
        Vector2 start,
        Vector2 target,
        LayerMask wallMask,
        out RaycastHit2D blockingHit)
    {
        blockingHit = default;
        Vector2 movement = target - start;
        float distance = movement.magnitude;
        if (wallMask.value == 0 || distance <= MinimumMoveDistance)
            return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(start, movement / distance, distance, wallMask);
        float nearestDistance = float.PositiveInfinity;
        bool foundHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null || collider.isTrigger)
                continue;
            if (collider.GetComponentInParent<PlatformEffector2D>() != null)
                continue;
            if (hits[i].distance >= nearestDistance)
                continue;

            blockingHit = hits[i];
            nearestDistance = hits[i].distance;
            foundHit = true;
        }

        return foundHit;
    }
}
