using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GourmetAbyss.CameraSystem.Acceptance
{
    /// <summary>
    /// 直接加载正式场景的需求验收。这里不创建替代场景，验证的是策划实际会玩的
    /// UpGround / Layer1 以及其中真实序列化的 CameraFollow、玩家和餐厅入口。
    /// </summary>
    public sealed class CameraRequirementsAcceptanceTests
    {
        private const string TownScene = "UpGround";
        private const string DungeonScene = "Layer1";
        private readonly List<string> _cameraErrors = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureCameraError;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CameraDirector director = CameraService.Active;
            if (director != null && director.InputRouter != null)
                director.InputRouter.ClearDebugOverride();

            Application.logMessageReceived -= CaptureCameraError;
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TownScene_ActuallyFollowsFacingDirectionWithSmoothLookAhead()
        {
            yield return LoadProductionScene(TownScene);
            CameraDirector director = RequireActiveDirector(TownScene);
            MonoBehaviour cameraFollow = RequireBehaviour("CameraFollow");
            Transform target = RequireDefaultTarget(cameraFollow);
            FreezePlayer(target);

            Assert.That(director.Camera.orthographic, Is.True, "小镇正式场景必须使用正交相机。");
            Assert.That(director.GetDebugSummary(), Does.Contain("Town"), "UpGround 未启用小镇镜头源。");

            CameraPlane plane = CameraPlane.FromRotation(director.CurrentPose.Rotation, target.position);
            MonoBehaviour controller = FindBehaviourOnTarget(target, "TopDownController");
            Assert.That(controller, Is.Not.Null, "小镇默认跟随目标缺少 TopDownController。");

            SetPrivateField(controller, "cameraFacingDirection", plane.Right);
            yield return WaitRealtime(1.4f);
            CameraPose facingRightPose = director.CurrentPose;

            SetPrivateField(controller, "cameraFacingDirection", -plane.Right);
            yield return WaitRealtime(1.4f);
            CameraPose facingLeftPose = director.CurrentPose;

            float lookAheadTravel = Mathf.Abs(Vector3.Dot(
                facingRightPose.Position - facingLeftPose.Position,
                plane.Right));
            Assert.That(lookAheadTravel, Is.GreaterThan(1.5f),
                "改变玩家朝向后，小镇镜头没有产生足够的朝向前瞻位移。");
            Assert.That(lookAheadTravel, Is.LessThan(3.6f),
                "小镇镜头朝向前瞻超过配置上限。");
            AssertNoCameraErrors();
        }

        [UnityTest]
        public IEnumerator DungeonScene_ActuallyUsesDeadZoneUiBlockingAndCappedMouseOffset()
        {
            yield return LoadProductionScene(DungeonScene);
            CameraDirector director = RequireActiveDirector(DungeonScene);
            MonoBehaviour cameraFollow = RequireBehaviour("CameraFollow");
            Transform target = RequireDefaultTarget(cameraFollow);
            FreezePlayer(target);

            Assert.That(director.Camera.orthographic, Is.True, "地牢正式场景必须使用正交相机。");
            Assert.That(Mathf.Abs((director.CurrentPose.Rotation * Vector3.forward).y), Is.GreaterThan(0.1f),
                "地牢正式场景相机缺少俯角。");
            Assert.That(director.GetDebugSummary(), Does.Contain("Dungeon"), "Layer1 未启用地牢镜头源。");

            CameraInputRouter router = director.InputRouter;
            router.SetDebugOverride(new CameraInputFrame { PointerNormalized = Vector2.zero });
            yield return WaitRealtime(1.3f);
            CameraPose centeredPose = director.CurrentPose;

            router.SetDebugOverride(new CameraInputFrame { PointerNormalized = new Vector2(8f, 0f) });
            yield return WaitRealtime(1.5f);
            CameraPose edgePose = director.CurrentPose;

            CameraPlane plane = CameraPlane.FromRotation(edgePose.Rotation, target.position);
            Vector3 edgeDelta = edgePose.Position - centeredPose.Position;
            float edgeOffset = new Vector2(
                Vector3.Dot(edgeDelta, plane.Right),
                Vector3.Dot(edgeDelta, plane.Up)).magnitude;
            Assert.That(edgeOffset, Is.GreaterThan(2f), "鼠标移至屏幕边缘后地牢镜头偏移不足。");
            Assert.That(edgeOffset, Is.LessThanOrEqualTo(3.15f), "地牢镜头偏移超过 3 米配置上限。");

            router.SetDebugOverride(new CameraInputFrame
            {
                PointerNormalized = Vector2.right,
                PointerBlockedByUi = true
            });
            yield return WaitRealtime(1.5f);
            float blockedOffset = Vector3.ProjectOnPlane(
                director.CurrentPose.Position - centeredPose.Position,
                plane.Normal).magnitude;
            Assert.That(blockedOffset, Is.LessThan(0.35f), "鼠标位于 UI 上时地牢镜头仍在跟随鼠标。");
            AssertNoCameraErrors();
        }

        [UnityTest]
        public IEnumerator RestaurantInTown_ActuallyLocksCentersPansBoundsAndRestores()
        {
            yield return LoadProductionScene(TownScene);
            CameraDirector director = RequireActiveDirector(TownScene);
            MonoBehaviour cameraFollow = RequireBehaviour("CameraFollow");
            Transform player = RequireDefaultTarget(cameraFollow);
            MonoBehaviour controller = FindBehaviourOnTarget(player, "TopDownController");
            MonoBehaviour restaurant = RequireBehaviour("RestaurantEntryPoint");
            Assert.That(controller, Is.Not.Null, "餐厅验收找不到正式玩家控制器。");

            bool originalCanMove = (bool)GetField(controller, "canPlayerMove");
            int baseRequestCount = director.RequestCount;
            CameraPose basePose = director.CurrentPose;

            SetPrivateField(restaurant, "_cachedPlayer", controller);
            InvokePrivate(restaurant, "EnterEntryState");
            yield return WaitRealtime(1.5f);

            Assert.That((bool)GetProperty(restaurant, "IsEntered"), Is.True, "正式餐厅入口未进入餐厅状态。");
            Assert.That((bool)GetField(controller, "canPlayerMove"), Is.False, "进入餐厅后玩家仍可移动。");
            Assert.That(director.RequestCount, Is.EqualTo(baseRequestCount + 1), "餐厅镜头请求没有正确入栈。");
            Assert.That(director.GetDebugSummary(), Does.Contain("Restaurant"), "餐厅镜头未取得控制权。");

            CameraInputRouter router = director.InputRouter;
            CameraPose beforeDrag = director.CurrentPose;
            GameObject runtimeCameraAnchor = GetField(restaurant, "_runtimeCameraAnchor") as GameObject;
            Assert.That(runtimeCameraAnchor, Is.Not.Null, "餐厅镜头没有创建运行时锚点。");
            CameraPlane plane = CameraPlane.FromRotation(
                beforeDrag.Rotation,
                runtimeCameraAnchor.transform.position);
            CameraPlanarBounds bounds = (CameraPlanarBounds)InvokePrivate(restaurant, "ResolveRestaurantBounds", plane);
            Assert.That(bounds.IsValid, Is.True, "正式餐厅没有可用的相机边界。");

            Vector2 dragInput = FindDragInputForLargestAvailablePan(
                beforeDrag,
                director.Camera.aspect,
                plane,
                bounds,
                out float availablePan);
            router.SetDebugOverride(new CameraInputFrame
            {
                PanHeld = true,
                PointerDeltaPixels = dragInput
            });
            yield return null;
            router.SetDebugOverride(new CameraInputFrame { PanHeld = true });
            yield return WaitRealtime(1.1f);

            CameraPose afterDrag = director.CurrentPose;
            float actualPan = Vector3.Distance(afterDrag.Position, beforeDrag.Position);
            if (availablePan > 0.1f)
            {
                Assert.That(actualPan, Is.GreaterThan(0.1f),
                    "正式餐厅内容超出一屏，但中键拖拽没有移动镜头。");
            }
            else
            {
                Assert.That(actualPan, Is.LessThan(0.1f),
                    "正式餐厅内容可完整显示时，镜头不应被拖离中心。");
            }

            CameraPose constrained = CameraBoundsUtility.ConstrainOrthographicPose(
                afterDrag,
                director.Camera.aspect,
                plane,
                bounds);
            Assert.That(Vector3.Distance(constrained.Position, afterDrag.Position), Is.LessThan(0.02f),
                "餐厅拖拽后镜头越过了餐厅边界。");

            InvokePublic(restaurant, "LeaveRestaurant");
            yield return WaitRealtime(1.3f);
            Assert.That((bool)GetProperty(restaurant, "IsEntered"), Is.False, "离开餐厅后状态未恢复。");
            Assert.That((bool)GetField(controller, "canPlayerMove"), Is.EqualTo(originalCanMove),
                "离开餐厅后玩家移动状态没有恢复。");
            Assert.That(director.RequestCount, Is.EqualTo(baseRequestCount), "离开餐厅后镜头请求残留。");
            Assert.That(Vector3.Distance(director.CurrentPose.Position, basePose.Position), Is.LessThan(1.5f),
                "离开餐厅后镜头没有回到进入前的跟随构图。");
            AssertNoCameraErrors();
        }

        private static Vector2 FindDragInputForLargestAvailablePan(
            CameraPose centeredPose,
            float aspect,
            CameraPlane plane,
            CameraPlanarBounds bounds,
            out float availablePan)
        {
            Vector2[] directions =
            {
                Vector2.right,
                Vector2.left,
                Vector2.up,
                Vector2.down
            };

            availablePan = 0f;
            Vector2 bestDirection = Vector2.right;
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2 direction = directions[i];
                CameraPose probe = centeredPose;
                probe.Position += plane.Right * (direction.x * 1000f) +
                                  plane.Up * (direction.y * 1000f);
                CameraPose constrained = CameraBoundsUtility.ConstrainOrthographicPose(
                    probe,
                    aspect,
                    plane,
                    bounds);
                Vector3 displacement = constrained.Position - centeredPose.Position;
                float planarDistance = new Vector2(
                    Vector3.Dot(displacement, plane.Right),
                    Vector3.Dot(displacement, plane.Up)).magnitude;
                if (planarDistance <= availablePan)
                    continue;

                availablePan = planarDistance;
                bestDirection = direction;
            }

            // RestaurantPanCameraSource uses grab-and-drag semantics, so pointer motion is
            // the inverse of the requested camera travel direction.
            return -bestDirection * 320f;
        }

        [UnityTest]
        public IEnumerator ProductionSceneTransition_ActuallyRebindsTownDungeonTownWithoutLeaks()
        {
            yield return LoadProductionScene(TownScene);
            CameraDirector firstTown = RequireActiveDirector(TownScene);
            int firstTownId = firstTown.GetInstanceID();
            Assert.That(firstTown.GetDebugSummary(), Does.Contain("Town"));

            yield return LoadProductionScene(DungeonScene);
            CameraDirector dungeon = RequireActiveDirector(DungeonScene);
            Assert.That(dungeon.GetInstanceID(), Is.Not.EqualTo(firstTownId), "切到地牢后仍引用已卸载的小镇相机。");
            Assert.That(dungeon.GetDebugSummary(), Does.Contain("Dungeon"));
            Assert.That(dungeon.RequestCount, Is.EqualTo(1), "地牢场景基础镜头请求数量异常。");

            int dungeonId = dungeon.GetInstanceID();
            yield return LoadProductionScene(TownScene);
            CameraDirector secondTown = RequireActiveDirector(TownScene);
            Assert.That(secondTown.GetInstanceID(), Is.Not.EqualTo(dungeonId), "返回小镇后仍引用已卸载的地牢相机。");
            Assert.That(secondTown.GetDebugSummary(), Does.Contain("Town"));
            Assert.That(secondTown.RequestCount, Is.EqualTo(1), "返回小镇后存在跨场景镜头请求泄漏。");
            AssertNoCameraErrors();
        }

        private IEnumerator LoadProductionScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, $"无法加载正式场景 {sceneName}。");
            while (!operation.isDone)
                yield return null;

            float timeoutAt = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                CameraDirector director = CameraService.Active;
                MonoBehaviour follow = FindBehaviour("CameraFollow");
                if (director != null && follow != null && TryGetDefaultTarget(follow, out _))
                    break;
                yield return null;
            }

            yield return WaitRealtime(0.4f);
        }

        private static CameraDirector RequireActiveDirector(string sceneName)
        {
            CameraDirector director = CameraService.Active;
            Assert.That(director, Is.Not.Null, $"正式场景 {sceneName} 没有激活 CameraDirector。");
            Assert.That(director.RequestCount, Is.GreaterThanOrEqualTo(1), $"正式场景 {sceneName} 没有基础镜头请求。");
            return director;
        }

        private static MonoBehaviour RequireBehaviour(string typeName)
        {
            MonoBehaviour behaviour = FindBehaviour(typeName);
            Assert.That(behaviour, Is.Not.Null, $"正式场景中找不到 {typeName}。");
            return behaviour;
        }

        private static MonoBehaviour FindBehaviour(string typeName)
        {
            return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
        }

        private static MonoBehaviour FindBehaviourOnTarget(Transform target, string typeName)
        {
            if (target == null)
                return null;
            return target.GetComponentsInParent<MonoBehaviour>(true)
                .Concat(target.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(item => item != null && item.GetType().Name == typeName);
        }

        private static Transform RequireDefaultTarget(MonoBehaviour cameraFollow)
        {
            Assert.That(TryGetDefaultTarget(cameraFollow, out Transform target), Is.True,
                "CameraFollow 没有绑定正式玩家目标。");
            return target;
        }

        private static bool TryGetDefaultTarget(MonoBehaviour cameraFollow, out Transform target)
        {
            target = cameraFollow?.GetType()
                .GetProperty("DefaultTarget", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(cameraFollow) as Transform;
            return target != null;
        }

        private static void FreezePlayer(Transform target)
        {
            MonoBehaviour controller = FindBehaviourOnTarget(target, "TopDownController");
            if (controller != null)
                controller.enabled = false;
            Rigidbody body = target.GetComponentInParent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} 缺少字段 {fieldName}。");
            return field.GetValue(target);
        }

        private static object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{target.GetType().Name} 缺少属性 {propertyName}。");
            return property.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} 缺少私有字段 {fieldName}。");
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            return Invoke(target, methodName, BindingFlags.Instance | BindingFlags.NonPublic, arguments);
        }

        private static object InvokePublic(object target, string methodName, params object[] arguments)
        {
            return Invoke(target, methodName, BindingFlags.Instance | BindingFlags.Public, arguments);
        }

        private static object Invoke(object target, string methodName, BindingFlags flags, object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, flags);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name} 缺少方法 {methodName}。");
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private void CaptureCameraError(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            bool belongsToCamera = condition.IndexOf("[Camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   stackTrace.IndexOf("GourmetAbyss.CameraSystem", StringComparison.OrdinalIgnoreCase) >= 0;
            if (belongsToCamera)
                _cameraErrors.Add(condition);
        }

        private void AssertNoCameraErrors()
        {
            Assert.That(_cameraErrors, Is.Empty,
                "正式场景运行期间出现镜头框架错误：\n" + string.Join("\n", _cameraErrors));
        }
    }
}
