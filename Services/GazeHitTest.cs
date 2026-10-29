using System.Numerics;

namespace HololensSatelliteViewer.Services
{
    internal static class GazeHitTest
    {
        internal static bool Hits(Vector3 origin, Vector3 direction, Vector3 target, float radius)
        {
            float directionLengthSquared = direction.LengthSquared();
            if (radius <= 0f || directionLengthSquared < 1e-8f)
                return false;

            direction = Vector3.Normalize(direction);
            Vector3 toTarget = target - origin;
            float distanceAlongRay = Vector3.Dot(toTarget, direction);
            if (distanceAlongRay < 0f)
                return false;

            Vector3 closestPoint = origin + direction * distanceAlongRay;
            return (target - closestPoint).LengthSquared() < radius * radius;
        }
    }
}