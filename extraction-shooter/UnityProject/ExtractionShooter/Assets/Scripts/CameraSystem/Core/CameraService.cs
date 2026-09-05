using UnityEngine;
using System.Collections.Generic;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>业务层使用的稳定相机入口。</summary>
    public static class CameraService
    {
        private static readonly List<CameraDirector> Directors = new List<CameraDirector>();
        public static CameraDirector Active { get; private set; }

        internal static void Register(CameraDirector director)
        {
            if (director == null)
                return;

            if (!Directors.Contains(director))
                Directors.Add(director);
            RefreshActive();
        }

        internal static void Unregister(CameraDirector director)
        {
            Directors.Remove(director);
            RefreshActive();
        }

        public static CameraShotLease AcquireShot(
            Object owner,
            ICameraShotSource source,
            CameraShotOptions options)
        {
            return Active != null ? Active.AcquireShot(owner, source, options) : null;
        }

        public static void PlayImpulse(float duration, float magnitude, float frequency = 24f)
        {
            Active?.PlayImpulse(duration, magnitude, frequency);
        }

        private static void RefreshActive()
        {
            for (int i = Directors.Count - 1; i >= 0; i--)
            {
                if (Directors[i] == null)
                    Directors.RemoveAt(i);
            }

            Camera main = Camera.main;
            if (main != null)
            {
                CameraDirector mainDirector = main.GetComponent<CameraDirector>();
                if (mainDirector != null && mainDirector.isActiveAndEnabled)
                {
                    Active = mainDirector;
                    return;
                }
            }

            for (int i = Directors.Count - 1; i >= 0; i--)
            {
                CameraDirector candidate = Directors[i];
                if (candidate != null && candidate.isActiveAndEnabled && candidate.Camera != null)
                {
                    Active = candidate;
                    return;
                }
            }

            Active = null;
        }
    }
}
