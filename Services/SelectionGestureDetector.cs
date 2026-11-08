using LunaCopy.Native;

namespace LunaCopy.Services;

public static class SelectionGestureDetector
{
    public const double DefaultDragThreshold = 5.0;
    public const double DefaultDoubleClickMaxDistance = 5.0;
    public const uint DefaultDoubleClickTimeMs = 500;

    public static double GetDistance(NativeMethods.POINT a, NativeMethods.POINT b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool IsDragSelection(NativeMethods.POINT down, NativeMethods.POINT up, double minDistance = DefaultDragThreshold)
    {
        return GetDistance(down, up) > minDistance;
    }

    public static bool IsDoubleClick(
        uint lastClickUpTime,
        uint currentClickTime,
        NativeMethods.POINT lastClickUpPoint,
        NativeMethods.POINT currentPoint,
        uint maxDoubleClickTime = DefaultDoubleClickTimeMs,
        double maxDistance = DefaultDoubleClickMaxDistance)
    {
        if (lastClickUpTime == 0) return false;

        uint timeDiff = unchecked(currentClickTime - lastClickUpTime);
        if (timeDiff > maxDoubleClickTime) return false;

        double distance = GetDistance(lastClickUpPoint, currentPoint);
        return distance <= maxDistance;
    }
}
