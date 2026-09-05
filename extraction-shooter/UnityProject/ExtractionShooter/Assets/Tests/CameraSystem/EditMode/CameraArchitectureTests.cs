using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GourmetAbyss.CameraSystem.Tests
{
    public class CameraArchitectureTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void GameplayCameraLensHasSingleWriter()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            string[] offenders = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("CameraDirector.cs"))
                .Where(path => !path.EndsWith("SkillTreeCameraController.cs"))
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\.orthographicSize\s*="))
                .Select(path => path.Replace(ProjectRoot + Path.DirectorySeparatorChar, string.Empty))
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "Gameplay camera lens must only be written by CameraDirector. Offenders: " + string.Join(", ", offenders));
        }

        [Test]
        public void NoGameplayCodeUsesLegacyStaticFocusRequests()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            string[] offenders = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("CameraFollow.cs"))
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"CameraFollow\.(Push|Pop)(XFocus|YFocus|OrthoSize)Request"))
                .Select(path => path.Replace(ProjectRoot + Path.DirectorySeparatorChar, string.Empty))
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "Legacy static camera requests must be migrated to leases. Offenders: " + string.Join(", ", offenders));
        }

        [Test]
        public void GameplayBuildScenesContainCameraFollowBootstrap()
        {
            string metaPath = Path.Combine(Application.dataPath, "Scripts", "3C", "CameraFollow.cs.meta");
            string guidLine = File.ReadLines(metaPath).First(line => line.StartsWith("guid:"));
            string guid = guidLine.Substring("guid:".Length).Trim();

            string[] missing = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Where(scene => !scene.path.EndsWith("MainUI.unity"))
                .Where(scene => !File.ReadAllText(Path.Combine(ProjectRoot, scene.path)).Contains(guid))
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(
                missing,
                Is.Empty,
                "Gameplay scene has no CameraFollow bootstrap: " + string.Join(", ", missing));
        }
    }
}
