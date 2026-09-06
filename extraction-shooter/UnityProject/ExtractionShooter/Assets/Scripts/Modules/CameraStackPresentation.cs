using UnityEngine;

namespace Game.Modules
{
    // Rendering compatibility only; the gameplay camera remains owned by CameraDirector.
    [DefaultExecutionOrder(1200)]
    public sealed class CameraStackPresentation : MonoBehaviour
    {
        public Camera source;
        public Camera[] overlays = System.Array.Empty<Camera>();
        public Behaviour[] orthographicOnly = System.Array.Empty<Behaviour>();
        private bool[] originalEnabled;
        private bool suspended;
        private GourmetAbyss.CameraSystem.CameraDirector director;
        private void Start()
        {
            director = source != null ? source.GetComponent<GourmetAbyss.CameraSystem.CameraDirector>() : null;
            if (director != null) director.SetProjectionFollowers(overlays);
        }
        private void OnEnable() { if (director != null) director.SetProjectionFollowers(overlays); }
        private void LateUpdate()
        {
            if (source == null) return;
            if (!source.orthographic && !suspended)
            {
                suspended = true; originalEnabled = new bool[orthographicOnly.Length];
                for (int i = 0; i < orthographicOnly.Length; i++)
                    if (orthographicOnly[i] != null) { originalEnabled[i] = orthographicOnly[i].enabled; orthographicOnly[i].enabled = false; }
            }
            else if (source.orthographic && suspended) Restore();
        }
        private void Restore()
        {
            if (!suspended) return;
            for (int i = 0; i < orthographicOnly.Length && i < originalEnabled.Length; i++)
                if (orthographicOnly[i] != null) orthographicOnly[i].enabled = originalEnabled[i];
            suspended = false;
        }
        private void OnDisable() { Restore(); if (director != null) director.SetProjectionFollowers(null); }
    }
}
