using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    public static class CameraBoundsUtility
    {
        public static bool TryCollectRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        public static CameraPose ConstrainOrthographicPose(
            CameraPose pose,
            float aspect,
            CameraPlane plane,
            CameraPlanarBounds bounds)
        {
            if (!bounds.IsValid)
                return pose;

            Vector3 forward = pose.Rotation * Vector3.forward;
            if (!plane.TryRaycast(pose.Position, forward, out Vector3 centerHit))
                return pose;

            float halfHeight = Mathf.Max(0.01f, pose.OrthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            Vector3 cameraRight = pose.Rotation * Vector3.right;
            Vector3 cameraUp = pose.Rotation * Vector3.up;

            Vector2 footprintMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 footprintMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Vector2 centerPlane = plane.ToPlane(centerHit);

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 rayOrigin = pose.Position + cameraRight * (x * halfWidth) + cameraUp * (y * halfHeight);
                if (!plane.TryRaycast(rayOrigin, forward, out Vector3 hit))
                    return pose;

                Vector2 relative = plane.ToPlane(hit) - centerPlane;
                footprintMin = Vector2.Min(footprintMin, relative);
                footprintMax = Vector2.Max(footprintMax, relative);
            }

            float allowedMinX = bounds.Min.x - footprintMin.x;
            float allowedMaxX = bounds.Max.x - footprintMax.x;
            float allowedMinY = bounds.Min.y - footprintMin.y;
            float allowedMaxY = bounds.Max.y - footprintMax.y;

            float clampedX = allowedMinX <= allowedMaxX
                ? Mathf.Clamp(centerPlane.x, allowedMinX, allowedMaxX)
                : bounds.Center.x;
            float clampedY = allowedMinY <= allowedMaxY
                ? Mathf.Clamp(centerPlane.y, allowedMinY, allowedMaxY)
                : bounds.Center.y;

            Vector2 correction = new Vector2(clampedX, clampedY) - centerPlane;
            pose.Position += plane.Right * correction.x + plane.Up * correction.y;
            return pose;
        }
    }
}
