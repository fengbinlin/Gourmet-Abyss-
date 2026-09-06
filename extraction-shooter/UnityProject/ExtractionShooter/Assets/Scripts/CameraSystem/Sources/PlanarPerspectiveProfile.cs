using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    [CreateAssetMenu(menuName = "Game/Modules/Perspective Profile")]
    public sealed class PlanarPerspectiveProfile : ScriptableObject
    {
        [Range(10, 75)] public float tiltFromNormal = 32f;
        [Range(15, 70)] public float fieldOfView = 38f;
        [Min(1)] public float distance = 28f;
        [Min(0)] public float panLimit = 3f;
        [Min(0)] public float dragSensitivity = 1f;
        public int priority = 60;
    }
}
