using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Modules
{
    public sealed class ModuleHUD : MonoBehaviour
    {
        [Serializable] public struct ActionButton { public string id; public Button button; }
        [Serializable] public struct TextElement { public string id; public Text text; }
        [Serializable] public struct ImageElement { public string id; public Image image; }
        [Serializable] public struct Region { public string id; public RectTransform root; }
        public ActionButton[] actions = Array.Empty<ActionButton>();
        public TextElement[] texts = Array.Empty<TextElement>();
        public ImageElement[] images = Array.Empty<ImageElement>();
        public Region[] regions = Array.Empty<Region>();
        public Text GetText(string id) { foreach(var x in texts) if(x.id==id)return x.text;return null; }
        public Image GetImage(string id) { foreach(var x in images) if(x.id==id)return x.image;return null; }
        public RectTransform GetRegion(string id) { foreach(var x in regions) if(x.id==id)return x.root;return null; }
        public Button GetAction(string id)
        {
            foreach (var action in actions) if (action.id == id) return action.button;
            return null;
        }
    }
}
