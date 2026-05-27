using UnityEngine;

public static class WeaponAimConstraintSolver
{
    public static Vector2 SolveReachablePosition(
        Vector2 origin,
        Vector2 desiredWorldPosition,
        float controlRadius,
        LayerMask wallMask,
        ref Vector2 lastReachablePosition,
        bool hasLastReachablePosition)
    {
        Vector2 desired = ClampToControlRadius(origin, desiredWorldPosition, controlRadius);

        if (IsReachable(origin, desired, wallMask))
        {
            lastReachablePosition = desired;
            return desired;
        }

        if (!hasLastReachablePosition)
        {
            Vector2 fallback = SolveBlockedFallback(origin, desired, controlRadius, wallMask);
            lastReachablePosition = fallback;
            return fallback;
        }

        Vector2 last = ClampToControlRadius(origin, lastReachablePosition, controlRadius);

        Vector2 xOnly = new Vector2(desired.x, last.y);
        xOnly = ClampToControlRadius(origin, xOnly, controlRadius);

        Vector2 yOnly = new Vector2(last.x, desired.y);
        yOnly = ClampToControlRadius(origin, yOnly, controlRadius);

        bool canMoveX = IsReachable(origin, xOnly, wallMask);
        bool canMoveY = IsReachable(origin, yOnly, wallMask);

        if (canMoveX && canMoveY)
        {
            Vector2 delta = desired - last;
            Vector2 result = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? xOnly : yOnly;
            lastReachablePosition = result;
            return result;
        }

        if (canMoveX)
        {
            lastReachablePosition = xOnly;
            return xOnly;
        }

        if (canMoveY)
        {
            lastReachablePosition = yOnly;
            return yOnly;
        }

        lastReachablePosition = last;
        return last;
    }

    private static Vector2 ClampToControlRadius(Vector2 origin, Vector2 position, float controlRadius)
    {
        float radius = Mathf.Max(0f, controlRadius);
        if (radius <= 0f)
            return origin;

        Vector2 offset = position - origin;
        float distance = offset.magnitude;

        if (distance <= radius)
            return position;

        return origin + offset.normalized * radius;
    }

    private static bool IsReachable(Vector2 origin, Vector2 target, LayerMask wallMask)
    {
        if (wallMask.value == 0)
            return true;

        Vector2 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
            return true;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction.normalized,
            distance,
            wallMask
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null)
                continue;

            if (IsIgnoredLineOfSightCollider(col))
                continue;

            return false;
        }

        return true;
    }

    private static Vector2 SolveBlockedFallback(
        Vector2 origin,
        Vector2 desired,
        float controlRadius,
        LayerMask wallMask)
    {
        Vector2 direction = desired - origin;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
            return origin;

        Vector2 clamped = ClampToControlRadius(origin, desired, controlRadius);

        if (wallMask.value == 0)
            return clamped;

        Vector2 clampedDirection = clamped - origin;
        float clampedDistance = clampedDirection.magnitude;

        if (clampedDistance <= 0.0001f)
            return origin;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            clampedDirection.normalized,
            clampedDistance,
            wallMask
        );

        RaycastHit2D? nearestBlockingHit = null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null)
                continue;

            if (IsIgnoredLineOfSightCollider(col))
                continue;

            if (nearestBlockingHit == null || hits[i].distance < nearestBlockingHit.Value.distance)
                nearestBlockingHit = hits[i];
        }

        if (nearestBlockingHit == null)
            return clamped;

        return nearestBlockingHit.Value.point;
    }

    private static bool IsIgnoredLineOfSightCollider(Collider2D col)
    {
        if (col == null)
            return true;

        if (col.isTrigger)
            return true;

        if (col.GetComponentInParent<PlatformEffector2D>() != null)
            return true;

        return false;
    }
}
