using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Modules
{
    // Scene integration only. Never put references to another module in a presentation prefab.
    [DefaultExecutionOrder(900)]
    public sealed class ModulePresentationScope : MonoBehaviour
    {
        public Camera targetCamera;
        public int rendererIndex = -1;
        public int rendererCount = 1;
        public Behaviour[] suspendWhileOpen = Array.Empty<Behaviour>();
        public CanvasGroup[] hideWhileOpen = Array.Empty<CanvasGroup>();
        public Renderer[] hideRenderersWhileOpen = Array.Empty<Renderer>();
        private bool[] rendererStates;
        private bool[] enabledStates;
        private struct Visibility { public float alpha; public bool interactable, blocksRaycasts; }
        private Visibility[] visibility;
        private UniversalAdditionalCameraData cameraData;
        private int originalRenderer;
        private bool opened;

        public void RegisterSuspended(Behaviour target)
        {
            if(target==null || Array.IndexOf(suspendWhileOpen,target)>=0)return;
            int index=suspendWhileOpen.Length;Array.Resize(ref suspendWhileOpen,index+1);suspendWhileOpen[index]=target;
            if(opened){Array.Resize(ref enabledStates,index+1);enabledStates[index]=target.enabled;target.enabled=false;}
        }

        public void SetOpen(bool open)
        {
            if (opened == open) return;
            if (!open) { Restore(); return; }
            opened = true;
            enabledStates = new bool[suspendWhileOpen.Length];
            rendererStates = new bool[hideRenderersWhileOpen.Length];
            for(int i=0;i<hideRenderersWhileOpen.Length;i++)
                if(hideRenderersWhileOpen[i]!=null)rendererStates[i]=hideRenderersWhileOpen[i].enabled;
            visibility = new Visibility[hideWhileOpen.Length];
            for (int i = 0; i < suspendWhileOpen.Length; i++)
                if (suspendWhileOpen[i] != null) { enabledStates[i] = suspendWhileOpen[i].enabled; suspendWhileOpen[i].enabled = false; }
            for (int i = 0; i < hideWhileOpen.Length; i++)
                if (hideWhileOpen[i] != null)
                {
                    var c = hideWhileOpen[i];
                    visibility[i] = new Visibility { alpha = c.alpha, interactable = c.interactable, blocksRaycasts = c.blocksRaycasts };
                }
            if (targetCamera != null && rendererIndex >= 0)
            {
                cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (cameraData != null && pipeline != null)
                {
                    originalRenderer = -1;
                    if (cameraData.scriptableRenderer != pipeline.scriptableRenderer)
                        for (int i = 0; i < rendererCount; i++)
                            if (cameraData.scriptableRenderer == pipeline.GetRenderer(i)) { originalRenderer = i; break; }
                    cameraData.SetRenderer(rendererIndex);
                }
            }
            ApplyVisibility();
        }
        private void LateUpdate() { if (opened) ApplyVisibility(); }
        private void ApplyVisibility()
        {
            foreach(var r in hideRenderersWhileOpen)if(r!=null)r.enabled=false;
            foreach (var c in hideWhileOpen)
                if (c != null) { c.alpha = 0; c.interactable = false; c.blocksRaycasts = false; }
        }
        private void Restore()
        {
            if (!opened) return;
            opened = false;
            if (cameraData != null) cameraData.SetRenderer(originalRenderer);
            for(int i=0;i<hideRenderersWhileOpen.Length&&i<rendererStates.Length;i++)
                if(hideRenderersWhileOpen[i]!=null)hideRenderersWhileOpen[i].enabled=rendererStates[i];
            for (int i = 0; i < suspendWhileOpen.Length && i < enabledStates.Length; i++)
                if (suspendWhileOpen[i] != null) suspendWhileOpen[i].enabled = enabledStates[i];
            for (int i = 0; i < hideWhileOpen.Length && i < visibility.Length; i++)
                if (hideWhileOpen[i] != null)
                { var c = hideWhileOpen[i]; c.alpha = visibility[i].alpha; c.interactable = visibility[i].interactable; c.blocksRaycasts = visibility[i].blocksRaycasts; }
            cameraData = null;
        }
        private void OnDisable() { Restore(); }
    }
}
