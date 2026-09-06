using UnityEngine;

namespace Game.Modules
{
    [CreateAssetMenu(menuName = "Game/Modules/Module Definition")]
    public sealed class ModuleDefinition : ScriptableObject
    {
        public string moduleId;
        public ModuleWorld worldPrefab;
        public ModuleHUD hudPrefab;
        [TextArea] public string requiredAnchorIds;
    }
}
