using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Modules
{
    // Presentation only: coordinates are resolved through each endpoint's own canvas.
    public static class ModuleScreenFlight
    {
        public static Vector2 ScreenPoint(Transform target, Vector3 fallback)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            var camera = canvas != null
                ? (canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera)
                : Camera.main;
            return RectTransformUtility.WorldToScreenPoint(camera, target != null ? target.position : fallback);
        }

        public static IEnumerator Play(Sprite icon, Vector2 fromScreen, Transform destination, float duration = .6f)
        {
            duration = Mathf.Max(.01f, duration);
            var host = new GameObject("ModuleScreenFlight", typeof(RectTransform), typeof(Canvas));
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var visual = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visual.transform.SetParent(host.transform, false);
            var image = visual.GetComponent<Image>(); image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            var rect = visual.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.one * (48f * Screen.height / 1080f);
            rect.anchoredPosition = fromScreen;
            // Also guarantees cleanup if the business host's coroutine is stopped on scene unload.
            Object.Destroy(host, duration + 1f);
            try
            {
                float elapsed = 0;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    if (host == null) yield break;
                    bool visible = destination != null && destination.gameObject.activeInHierarchy;
                    image.enabled = visible;
                    if (visible)
                    {
                        float t = Mathf.Clamp01(elapsed / duration);
                        rect.anchoredPosition = Vector2.Lerp(fromScreen, ScreenPoint(destination, destination.position), t)
                            + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 80f * Screen.height / 1080f);
                    }
                    yield return null;
                }
            }
            finally { if (host != null) Object.Destroy(host); }
        }
    }
}
