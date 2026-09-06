using System;
using UnityEngine;
using GourmetAbyss.CameraSystem;

namespace Game.Modules
{
    public sealed class ModuleWorld : MonoBehaviour
    {
        [Serializable] public struct Anchor { public string id; public Transform point; }
        public PlanarPerspectiveView view;
        public Anchor[] anchors = Array.Empty<Anchor>();
        public Transform GetAnchor(string id)
        {
            foreach (var anchor in anchors) if (anchor.id == id) return anchor.point;
            return null;
        }
    }
}
