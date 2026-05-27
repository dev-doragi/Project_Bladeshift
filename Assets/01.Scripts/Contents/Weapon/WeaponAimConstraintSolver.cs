using UnityEngine;

public static class WeaponAimConstraintSolver
{
    private const float NearOriginIgnoreDistance = 0.08f;
    private const float WallNormalXThreshold = 0.45f;
    private const float CeilingNormalYThreshold = -0.45f;
    private const float FallbackSkin = 0.04f;

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

        RaycastHit2D? blockingHit = FindBlockingHit(
            origin,
            direction.normalized,
            distance,
            wallMask
        );

        return !blockingHit.HasValue;
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

        Vector2 castDirection = clampedDirection.normalized;

        RaycastHit2D? blockingHit = FindBlockingHit(
            origin,
            castDirection,
            clampedDistance,
            wallMask
        );

        if (!blockingHit.HasValue)
            return clamped;

        return blockingHit.Value.point - castDirection * FallbackSkin;
    }

    private static RaycastHit2D? FindBlockingHit(
        Vector2 origin,
        Vector2 direction,
        float distance,
        LayerMask wallMask)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction,
            distance,
            wallMask
        );

        RaycastHit2D? result = null;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];

            if (!ShouldBlockReachHit(hit))
                continue;

            if (!result.HasValue || hit.distance < result.Value.distance)
                result = hit;
        }

        return result;
    }

    private static bool ShouldBlockReachHit(RaycastHit2D hit)
    {
        if (hit.collider == null)
            return false;

        if (hit.distance <= NearOriginIgnoreDistance)
            return false;

        Vector2 normal = hit.normal;

        if (Mathf.Abs(normal.x) >= WallNormalXThreshold)
            return true;

        if (normal.y <= CeilingNormalYThreshold)
            return true;

        return false;
    }
}