using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Modules.Editor
{
    public static class CombatProjectileProbe
    {
        public static string Status { get; private set; } = "Not started";
        [MenuItem("Tools/Modules/Test Combat Projectile (Play)")]
        public static void Begin()
        {
            if (!EditorApplication.isPlaying || Status == "Running") throw new InvalidOperationException("Run a dungeon first; do not run probes concurrently.");
            var player = Object.FindObjectOfType<TopDownController>();
            if (player == null || !player.GetCombatState()) throw new InvalidOperationException("Requires active combat player.");
            Status = "Running"; player.StartCoroutine(Run(player));
        }

        static IEnumerator Run(TopDownController player)
        {
            // MCP dispatch can stall a rendered frame; begin at a physics boundary, not in its catch-up interval.
            yield return new WaitForFixedUpdate();
            var weapon = player.GetComponentInChildren<PrimaryWeapon>(true);
            if (weapon == null || weapon.GetFirePoint() == null) { Status = "FAIL: weapon missing"; yield break; }
            var existing = Object.FindObjectsOfType<Projectile>().Select(p => p.GetInstanceID()).ToArray();
            var owned = Array.Empty<Projectile>();
            var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabas/Enemy/EnemtMush2D.prefab"));
            target.name = "CombatProbe_Target";
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var trace = new System.Collections.Generic.List<string>();
            Application.LogCallback log = (message, stack, type) => trace.Add(message);
            Application.logMessageReceived += log;
            try
            {
                foreach (var ai in target.GetComponentsInChildren<EnemyAI>()) ai.enabled = false;
                foreach (var body in target.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
                var collider = target.GetComponent<Collider>(); var health = target.GetComponent<EnemyHealth>();
                var hp = typeof(EnemyHealth).GetField("currentHealth", flags);
                // A disposable high-health target prevents drops/rewards. Player resources and saves are untouched.
                hp.SetValue(health, 100000f);
                var origin = weapon.GetFirePoint().position;
                var direction = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
                target.transform.position = origin + direction * 4f;
                Physics.SyncTransforms();
                target.transform.position += origin + direction * 4f - collider.bounds.center;
                Physics.SyncTransforms();
                Vector2 screen = Camera.main.WorldToScreenPoint(collider.bounds.center);
                bool resolved = player.TryResolveAimPoint(screen, out var aim, out var hit) && hit == collider;
                float before = (float)hp.GetValue(health);
                // Exercise the production weapon spawn + projectile collision path, without consuming a magazine.
                typeof(PrimaryWeapon).GetMethod("SpawnBullet", flags).Invoke(weapon, new object[] { origin, Quaternion.LookRotation(direction), direction });
                owned = Object.FindObjectsOfType<Projectile>().Where(p => !existing.Contains(p.GetInstanceID())).ToArray();
                bool emitted = owned.Length > 0;
                foreach (var projectile in owned)
                {
                    typeof(Projectile).GetField("debugMode", flags).SetValue(projectile, true);
                    trace.Add("Projectile invulnerableTime=" + typeof(Projectile).GetField("invulnerableTime", flags).GetValue(projectile));
                }
                yield return new WaitForSeconds(.06f);
                Directory.CreateDirectory("Library/CombatAcceptance");
                ScreenCapture.CaptureScreenshot("Library/CombatAcceptance/Projectile-probe.png");
                yield return new WaitForSeconds(1f);
                float after = health != null ? (float)hp.GetValue(health) : 0;
                Status = resolved && emitted && after < before ? "PASS" : "FAIL";
                File.WriteAllText("Library/CombatAcceptance/ProjectileReport.txt", Status +
                    "\nPlayer aim resolved test enemy=" + resolved + "; production projectile emitted=" + emitted +
                    "; target health=" + before + " -> " + after +
                    "\nDisposable target; production SpawnBullet/Projectile damage path. Does not verify input/ammo/reload/balance.\n" + string.Join("\n",trace));
                Debug.Log("[Combat projectile probe] " + Status);
            }
            finally
            {
                Application.logMessageReceived -= log;
                if (target != null) Object.Destroy(target);
                foreach (var projectile in owned)
                    if (projectile != null) Object.Destroy(projectile.gameObject);
                if (Status == "Running") Status = "FAIL: interrupted";
            }
        }
    }
}
