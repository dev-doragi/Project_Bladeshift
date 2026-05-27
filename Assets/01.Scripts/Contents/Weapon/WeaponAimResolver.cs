using UnityEngine;

public static class WeaponAimResolver
{
    public static Vector2 ResolveFallbackAimDirection(WeaponController controller)
    {
        if (controller != null && controller.PlayerAimDirection.sqrMagnitude > 0.0001f)
            return controller.PlayerAimDirection.normalized;

        if (controller != null && controller.transform.right.sqrMagnitude > 0.0001f)
            return ((Vector2)controller.transform.right).normalized;

        return Vector2.right;
    }

    public static float ResolveMeleeFacingSign(
        WeaponController controller,
        bool isGamepadInput,
        float gamepadDeadzone,
        Vector2 rawPointerWorldPosition)
    {
        if (controller == null || controller.PlayerTransform == null)
            return 1f;

        PlayerController playerController = controller.PlayerTransform.GetComponent<PlayerController>();
        float fallbackSign = playerController != null && playerController.FacingSign < 0f ? -1f : 1f;

        if (isGamepadInput)
        {
            InputReader input = InputReader.Instance;
            float gamepadLookX = input != null ? input.GetLookInput().x : 0f;
            if (Mathf.Abs(gamepadLookX) >= Mathf.Clamp01(gamepadDeadzone))
                return gamepadLookX >= 0f ? 1f : -1f;

            return fallbackSign;
        }

        float deltaX = rawPointerWorldPosition.x - controller.PlayerTransform.position.x;
        if (Mathf.Abs(deltaX) > 0.0001f)
            return deltaX >= 0f ? 1f : -1f;

        return fallbackSign;
    }

    public static bool TryResolveMouseThrustAim(
        Vector2 aimLockPosition,
        Vector2 aimMouseStartPosition,
        Vector2 mouseWorldPosition,
        float dragThreshold,
        out Vector2 direction,
        out Vector2 visualTarget,
        out bool canFire)
    {
        Vector2 delta = mouseWorldPosition - aimLockPosition;
        float dragDistance = Vector2.Distance(aimMouseStartPosition, mouseWorldPosition);
        visualTarget = mouseWorldPosition;
        canFire = dragDistance >= dragThreshold;

        if (delta.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.zero;
            return false;
        }

        direction = delta.normalized;
        return true;
    }

    public static bool TryResolveGamepadThrustAim(
        Vector2 fallbackDirection,
        float deadzone,
        float maxDistance,
        float dragThreshold,
        ref bool hasGamepadAimDirection,
        ref Vector2 gamepadAimDirection,
        out Vector2 direction,
        out Vector2 visualTarget,
        out bool canFire,
        out float dragDistance)
    {
        direction = hasGamepadAimDirection ? gamepadAimDirection : fallbackDirection;

        Vector2 lookInput = InputReader.Instance != null ? InputReader.Instance.GetLookInput() : Vector2.zero;
        float stickPower = 0f;
        float magnitude = Mathf.Clamp01(lookInput.magnitude);
        float clampedDeadzone = Mathf.Clamp01(deadzone);
        if (magnitude >= clampedDeadzone && lookInput.sqrMagnitude > 0.0001f)
        {
            direction = lookInput.normalized;
            gamepadAimDirection = direction;
            hasGamepadAimDirection = true;
            stickPower = Mathf.InverseLerp(clampedDeadzone, 1f, magnitude);
        }

        dragDistance = maxDistance * stickPower;
        canFire = dragDistance >= dragThreshold;
        visualTarget = direction * Mathf.Max(dragDistance, 0.01f);
        return true;
    }
}
