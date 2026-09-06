using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    public static class CameraAimUtility
    {
        // Same rules for both projections: collider first, then player-height ground plane.
        public static bool TryResolve(Camera camera, Vector2 screenPoint, int aimMask, int groundMask,
            float playerGroundHeight, float aimHeight, out Vector3 point, out Collider collider)
        {
            point = default; collider = null;
            if (camera == null) return false;
            var ray = camera.ScreenPointToRay(screenPoint);
            // Ground has a separate authored mask; it must participate even when aimMask lists enemies only.
            if (Physics.Raycast(ray, out var hit, 100f, aimMask | groundMask))
            {
                collider = hit.collider; point = hit.point;
                if ((groundMask & (1 << collider.gameObject.layer)) != 0) point += Vector3.up * aimHeight;
                return true;
            }
            var plane = new Plane(Vector3.up, new Vector3(0, playerGroundHeight, 0));
            if (!plane.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance) + Vector3.up * aimHeight;
            return true;
        }
    }
}
