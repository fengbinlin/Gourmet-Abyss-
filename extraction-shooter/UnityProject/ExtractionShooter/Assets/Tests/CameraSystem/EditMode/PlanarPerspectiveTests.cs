using NUnit.Framework;
using UnityEngine;

namespace GourmetAbyss.CameraSystem.Tests
{
    public class PlanarPerspectiveTests
    {
        [TestCase(0)]
        [TestCase(90)]
        public void View_UsesExplicitFrame_AndProjectsNearObjectsLarger(float groundRotation)
        {
            var go=new GameObject("PerspectiveFrameTest");var cameraGO=new GameObject("Camera");
            var profile=ScriptableObject.CreateInstance<PlanarPerspectiveProfile>();
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(10,20,30),Quaternion.Euler(groundRotation,0,0));
                var view=go.AddComponent<PlanarPerspectiveView>();view.frame=go.transform;view.profile=profile;
                var pose=view.Pose(Vector2.zero);var camera=cameraGO.AddComponent<Camera>();
                camera.orthographic=false;camera.fieldOfView=pose.FieldOfView;camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
                var center=camera.WorldToViewportPoint(go.transform.position);
                // Unity viewport conversion can include a subpixel center offset from the editor render target.
                Assert.That(center.x,Is.EqualTo(.5).Within(.001));Assert.That(center.y,Is.EqualTo(.5).Within(.001));
                var near=go.transform.position-go.transform.up*5;var far=go.transform.position+go.transform.up*5;
                float nearWidth=(camera.WorldToViewportPoint(near+go.transform.right)-camera.WorldToViewportPoint(near)).magnitude;
                float farWidth=(camera.WorldToViewportPoint(far+go.transform.right)-camera.WorldToViewportPoint(far)).magnitude;
                Assert.Greater(nearWidth,farWidth);
            }
            finally { Object.DestroyImmediate(go);Object.DestroyImmediate(cameraGO);Object.DestroyImmediate(profile); }
        }
    }
}
