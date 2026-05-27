using UnityEngine;

public static class PointerWorldPositionUtility
{
    public static bool TryGetMouseScreenPosition(
        InputReader inputReader,
        bool ignorePointerOutsideScreen,
        out Vector2 screenPosition)
    {
        screenPosition = default;

        if (inputReader == null)
            return false;

        screenPosition = inputReader.GetMousePosition();
        if (!ignorePointerOutsideScreen)
            return true;

        return !IsOutsideScreen(screenPosition);
    }

    public static bool TryGetMouseWorldPosition(
        Camera camera,
        InputReader inputReader,
        bool ignorePointerOutsideScreen,
        out Vector2 worldPosition)
    {
        worldPosition = default;
        if (camera == null)
            return false;

        if (!TryGetMouseScreenPosition(inputReader, ignorePointerOutsideScreen, out Vector2 screenPosition))
            return false;

        worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        return true;
    }

    private static bool IsOutsideScreen(Vector2 screenPosition)
    {
        return screenPosition.x < 0f ||
               screenPosition.y < 0f ||
               screenPosition.x > Screen.width ||
               screenPosition.y > Screen.height;
    }
}
