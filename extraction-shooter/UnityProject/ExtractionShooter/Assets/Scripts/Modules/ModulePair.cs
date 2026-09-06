using UnityEngine;

namespace Game.Modules
{
    public sealed class ModulePair : MonoBehaviour
    {
        public ModuleDefinition definition;
        public ModuleWorld world;
        public ModuleHUD hud;
        public ModulePresentationScope presentation;
        public bool IsOpen { get; private set; }
        public void SetOpen(bool open)
        {
            if (world == null || hud == null || world.view == null || IsOpen == open) return;
            IsOpen = open;
            if (presentation != null) presentation.SetOpen(open);
            hud.gameObject.SetActive(open);
            if (open) { if (!world.view.IsOpen) world.view.Open(); } else world.view.Close();
        }
        private void OnDisable() { SetOpen(false); }
    }
}
