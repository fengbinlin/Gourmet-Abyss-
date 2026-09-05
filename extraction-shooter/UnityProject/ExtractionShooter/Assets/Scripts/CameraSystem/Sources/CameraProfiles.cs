using UnityEngine;

namespace GourmetAbyss.CameraSystem
{
    public abstract class CameraSourceProfile : ScriptableObject
    {
        [Header("镜头")]
        [Tooltip("<= 0 时沿用场景相机当前值")]
        public float orthographicSize = -1f;
        public CameraDamping damping = new CameraDamping(0.3f);

        [Header("切换")]
        [Min(0f)] public float blendIn = 0.25f;
        [Min(0f)] public float blendOut = 0.25f;
    }

    [CreateAssetMenu(menuName = "料理地牢/Camera/Town Profile", fileName = "TownCameraProfile")]
    public sealed class TownCameraProfile : CameraSourceProfile
    {
        [Header("朝向前瞻")]
        [Min(0f)] public float lookAheadDistance = 1.5f;
        [Min(0f)] public float lookAheadSmoothTime = 0.2f;
    }

    [CreateAssetMenu(menuName = "料理地牢/Camera/Dungeon Profile", fileName = "DungeonCameraProfile")]
    public sealed class DungeonCameraProfile : CameraSourceProfile
    {
        [Header("鼠标偏移")]
        [Range(0f, 0.95f)] public float centerDeadZone = 0.1f;
        [Min(0f)] public float maxPointerOffset = 3f;
        [Range(0.25f, 4f)] public float responseExponent = 1.4f;
        [Min(0f)] public float pointerSmoothTime = 0.14f;
    }

    [CreateAssetMenu(menuName = "料理地牢/Camera/Restaurant Profile", fileName = "RestaurantCameraProfile")]
    public sealed class RestaurantCameraProfile : CameraSourceProfile
    {
        [Header("拖动")]
        [Min(0.01f)] public float dragSensitivity = 1f;
        public bool blockDragWhenPointerOverUi = true;
    }
}
