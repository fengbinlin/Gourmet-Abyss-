using UnityEngine;
using UnityEngine.UI;

namespace Game.Modules
{
    // Temporary host for a legacy screen panel. Restores the exact hierarchy and layout on close.
    public sealed class ModuleLegacyPopup : MonoBehaviour
    {
        public RectTransform content;
        public Canvas sourceCanvas;
        public Vector2 referenceSize = new Vector2(1920, 1080);
        private Transform originalParent;
        private int sibling;
        private Vector2 anchorMin, anchorMax, pivot, size, position;
        private Vector3 scale;
        private Quaternion rotation;
        private GameObject host;
        public event System.Action Closed;
        public bool IsOpen => host != null;
        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || content == null) return;
            originalParent=content.parent;sibling=content.GetSiblingIndex();
            anchorMin=content.anchorMin;anchorMax=content.anchorMax;pivot=content.pivot;
            size=content.sizeDelta;position=content.anchoredPosition;scale=content.localScale;rotation=content.localRotation;
            // Measure visible legacy graphics, not its often-empty 100x100 grouping RectTransform.
            var bounds = new Bounds(Vector3.zero,Vector3.zero); bool found=false;
            var corners=new Vector3[4];
            foreach(var graphic in content.GetComponentsInChildren<Graphic>())
            {
                graphic.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners)
                { var p=content.InverseTransformPoint(corner);if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p); }
            }
            host=new GameObject("LegacyModulePopup",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=referenceSize;scaler.matchWidthOrHeight=.5f;
            var dim=new GameObject("Dismiss",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));dim.transform.SetParent(host.transform,false);
            var rt=dim.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            dim.GetComponent<Image>().color=new Color(0,0,0,.65f);dim.GetComponent<Button>().onClick.AddListener(Close);
            content.SetParent(host.transform,false);content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=pivot;content.sizeDelta=size;content.localRotation=Quaternion.identity;
            float fit=Mathf.Min(1f,Mathf.Min((referenceSize.x-180)/Mathf.Max(bounds.size.x,1),(referenceSize.y-180)/Mathf.Max(bounds.size.y,1)));
            content.localScale=Vector3.one*fit;content.anchoredPosition=-(Vector2)bounds.center*fit;
            // Graphics intercept clicks within the old panel; clicking outside dismisses it.
        }
        public void Close()
        {
            if (!IsOpen) return;
            if(content!=null && originalParent!=null)
            {
                content.SetParent(originalParent,false);content.SetSiblingIndex(sibling);
                content.anchorMin=anchorMin;content.anchorMax=anchorMax;content.pivot=pivot;content.sizeDelta=size;
                content.anchoredPosition=position;content.localScale=scale;content.localRotation=rotation;
            }
            host.SetActive(false);Destroy(host);host=null;
            Closed?.Invoke();
        }
        private void OnDisable() { Close(); }
    }
}
