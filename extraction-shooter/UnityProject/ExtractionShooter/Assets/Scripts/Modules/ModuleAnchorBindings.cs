using System;
using UnityEngine;

namespace Game.Modules
{
    // Bind existing logic to a movable presentation; do not reparent or scale gameplay roots.
    [DefaultExecutionOrder(-200)]
    public sealed class ModuleAnchorBindings : MonoBehaviour
    {
        [Serializable] public struct Binding { public string anchorId; public Transform target; public Vector3 offset; }
        public ModuleWorld world;
        public Binding[] bindings = Array.Empty<Binding>();
        public void Apply()
        {
            if (world == null) return;
            foreach (var binding in bindings)
            {
                var anchor = world.GetAnchor(binding.anchorId);
                if (anchor != null && binding.target != null)
                    binding.target.position = anchor.position + world.view.frame.TransformVector(binding.offset);
            }
        }
        private void Awake() { Apply(); }
    }
}
