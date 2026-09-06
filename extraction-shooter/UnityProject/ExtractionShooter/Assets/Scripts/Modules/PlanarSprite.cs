using UnityEngine;
using GourmetAbyss.CameraSystem;

namespace Game.Modules
{
    // Foot is the parent transform. No scale is applied to gameplay or collision objects.
    [ExecuteAlways]
    public sealed class PlanarSprite : MonoBehaviour
    {
        public Transform frame;
        public PlanarPerspectiveProfile profile;
        public SpriteRenderer visual;
        public bool ground;
        public float orderOffset;
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (frame == null || visual == null || profile == null) return;
            transform.rotation = ground ? frame.rotation : frame.rotation * Quaternion.Euler(-profile.tiltFromNormal, 0f, 0f);
            float depth = Vector3.Dot(transform.position - frame.position, frame.up);
            visual.sortingOrder = ground ? -1000 + Mathf.RoundToInt(orderOffset) : Mathf.Clamp(Mathf.RoundToInt(-depth * 100f + orderOffset), -900, 900);
        }
    }
}
