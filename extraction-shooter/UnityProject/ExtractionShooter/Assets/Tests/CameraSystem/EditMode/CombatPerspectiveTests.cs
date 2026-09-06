using NUnit.Framework;
using UnityEngine;

namespace GourmetAbyss.CameraSystem.Tests
{
    public sealed class CombatPerspectiveTests
    {
        [Test]
        public void DungeonPerspective_UsesSamePointerCapWhenTargetMoves()
        {
            var target = new GameObject("target"); var cameraGO = new GameObject("camera");
            var profile = ScriptableObject.CreateInstance<DungeonPerspectiveProfile>();
            try
            {
                var camera = cameraGO.AddComponent<Camera>(); profile.pointerSmoothTime = 0;
                var source = profile.CreateSource(target.transform, Vector3.zero, Quaternion.identity, 10);
                var input = new CameraInputFrame { PointerNormalized = Vector2.one * 99 };
                var context = new CameraEvaluationContext(null, camera, input, default, .02f, .02f);
                Assert.IsTrue(source.TryEvaluate(context, out var first));
                Assert.IsTrue(first.Pose.Perspective); Assert.AreEqual(40, first.Pose.FieldOfView);
                var baseline = -(first.Pose.Rotation * Vector3.forward) * profile.distance;
                Assert.That((first.Pose.Position - baseline).magnitude, Is.EqualTo(3).Within(.001));
                target.transform.position = new Vector3(7, 0, 11);
                source.TryEvaluate(context, out var moving);
                Assert.That((moving.Pose.Position - target.transform.position - baseline).magnitude, Is.EqualTo(3).Within(.001));
                input.PointerNormalized = Vector2.one * .01f;
                context = new CameraEvaluationContext(null, camera, input, default, .02f, .02f);
                source.TryEvaluate(context, out var deadZone);
                Assert.That(Vector3.Distance(deadZone.Pose.Position, target.transform.position + baseline), Is.LessThan(.001));
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(cameraGO); Object.DestroyImmediate(profile); }
        }

        [TestCase(false)] [TestCase(true)]
        public void Aim_ResolvesGroundColliderEnemyColliderAndOutsideFallback(bool perspective)
        {
            var cameraGO = new GameObject("camera"); var ground = new GameObject("ground"); var enemy = new GameObject("enemy");
            try
            {
                var camera = cameraGO.AddComponent<Camera>(); camera.orthographic = !perspective;
                camera.fieldOfView = 40; camera.orthographicSize = 10; camera.aspect = 16f / 9;
                camera.transform.rotation = Quaternion.Euler(45, 0, 0); camera.transform.position = -camera.transform.forward * 27.5f;
                ground.layer = 28; var floor = ground.AddComponent<BoxCollider>(); floor.size = new Vector3(50, .2f, 50); ground.transform.position = Vector3.down * .1f;
                enemy.layer = 29; enemy.transform.position = new Vector3(3, 1, 4); enemy.AddComponent<BoxCollider>(); Physics.SyncTransforms();
                foreach (var p in new[] { new Vector3(-5,0,-5), Vector3.zero, new Vector3(5,0,5) })
                {
                    Assert.IsTrue(CameraAimUtility.TryResolve(camera, camera.WorldToScreenPoint(p), 1 << 29, 1 << 28, 0, 1.3f, out var aim, out var hit));
                    Assert.AreEqual(floor, hit); Assert.That(Vector3.Distance(aim, p + Vector3.up * 1.3f), Is.LessThan(.02));
                }
                Vector2 screen = camera.WorldToScreenPoint(enemy.transform.position);
                Assert.IsTrue(CameraAimUtility.TryResolve(camera, screen, (1 << 28) | (1 << 29), 1 << 28, 0, 1.3f, out var enemyAim, out var enemyHit));
                Assert.AreEqual(enemy.GetComponent<Collider>(), enemyHit);
                Assert.That(Vector2.Distance(camera.WorldToScreenPoint(enemyAim), screen), Is.LessThan(.1));
                var outside = new Vector3(-40,0,30);
                Assert.IsTrue(CameraAimUtility.TryResolve(camera, camera.WorldToScreenPoint(outside), 0, 1 << 28, 0, 1.3f, out var fallback, out var noCollider));
                Assert.IsNull(noCollider); Assert.That(Vector3.Distance(fallback, outside + Vector3.up * 1.3f), Is.LessThan(.02));
            }
            finally { Object.DestroyImmediate(cameraGO); Object.DestroyImmediate(ground); Object.DestroyImmediate(enemy); }
        }

        [Test]
        public void FocusRequest_PreservesPerspectiveLens()
        {
            var target = new GameObject("focus");
            try
            {
                var pose = new CameraPose(new Vector3(0,20,-20), Quaternion.Euler(45,0,0), 10, true, 40);
                var source = new TransformFocusCameraSource(target.transform, pose, 10, new CameraDamping(0));
                source.TryEvaluate(default, out var shot);
                Assert.IsTrue(shot.Pose.Perspective); Assert.AreEqual(40, shot.Pose.FieldOfView);
            }
            finally { Object.DestroyImmediate(target); }
        }
    }
}
