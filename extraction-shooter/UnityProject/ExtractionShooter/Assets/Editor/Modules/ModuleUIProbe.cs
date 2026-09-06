using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Modules.Editor
{
    // In-engine pointer injection: checks actual raycast ordering and interactability, not just UnityEvent wiring.
    public static class ModuleUIProbe
    {
        public static string Click(GameObject target)
        {
            if(target==null||!target.activeInHierarchy)throw new InvalidOperationException("UI target inactive or missing.");
            Canvas.ForceUpdateCanvases();
            var rect=target.GetComponent<RectTransform>();var canvas=target.GetComponentInParent<Canvas>();
            if(rect==null||canvas==null)throw new InvalidOperationException("Target has no UI rectangle/canvas.");
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            return ClickAt(point,target);
        }
        public static string ClickAt(Vector2 point,GameObject expected=null)
        {
            var events=EventSystem.current;if(events==null)throw new InvalidOperationException("No EventSystem.");
            var pointer=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();events.RaycastAll(pointer,hits);
            if(hits.Count==0)throw new InvalidOperationException("No UI hit at "+point);
            var hit=hits[0].gameObject;
            var receiver=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            if(receiver==null)throw new InvalidOperationException("Topmost UI does not handle clicks: "+hit.name);
            if(expected!=null&&receiver!=expected&&!receiver.transform.IsChildOf(expected.transform))
                throw new InvalidOperationException(expected.name+" blocked by "+receiver.name);
            var selectable=receiver.GetComponent<Selectable>();
            if(selectable!=null&&!selectable.IsInteractable())throw new InvalidOperationException("Disabled control: "+receiver.name);
            ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerClickHandler);
            return receiver.name;
        }
    }
}
