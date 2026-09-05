using NUnit.Framework;
using UnityEngine;

namespace GourmetAbyss.CameraSystem.Tests
{
    public class CameraMathTests
    {
        [Test]
        public void CameraPlane_FrontCameraUsesXYPlane()
        {
            CameraPlane plane = CameraPlane.FromRotation(Quaternion.identity, Vector3.zero);
            Assert.That(Vector3.Dot(plane.Normal, Vector3.forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(plane.Right, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(plane.Up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void CameraPlane_TiltedCameraUsesXZGround()
        {
            CameraPlane plane = CameraPlane.FromRotation(Quaternion.Euler(45f, 0f, 0f), Vector3.zero);
            Assert.That(Vector3.Dot(plane.Normal, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Mathf.Abs(Vector3.Dot(plane.Up, Vector3.forward)), Is.GreaterThan(0.999f));
        }

        [Test]
        public void BoundsConstraint_ClampsFrontOrthographicCamera()
        {
            CameraPose pose = new CameraPose(new Vector3(100f, 100f, -10f), Quaternion.identity, 5f);
            CameraPlane plane = CameraPlane.FromRotation(pose.Rotation, Vector3.zero);
            CameraPlanarBounds bounds = new CameraPlanarBounds(new Vector2(-10f, -10f), new Vector2(10f, 10f));

            CameraPose constrained = CameraBoundsUtility.ConstrainOrthographicPose(pose, 1f, plane, bounds);

            Assert.That(constrained.Position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(constrained.Position.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(constrained.Position.z, Is.EqualTo(-10f).Within(0.001f));
        }

        [Test]
        public void BoundsConstraint_CentersWhenViewIsLargerThanBounds()
        {
            CameraPose pose = new CameraPose(new Vector3(4f, 3f, -10f), Quaternion.identity, 20f);
            CameraPlane plane = CameraPlane.FromRotation(pose.Rotation, Vector3.zero);
            CameraPlanarBounds bounds = new CameraPlanarBounds(new Vector2(-5f, -5f), new Vector2(5f, 5f));

            CameraPose constrained = CameraBoundsUtility.ConstrainOrthographicPose(pose, 1f, plane, bounds);

            Assert.That(constrained.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(constrained.Position.y, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
