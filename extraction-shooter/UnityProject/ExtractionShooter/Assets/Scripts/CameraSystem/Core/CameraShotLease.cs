using System;

namespace GourmetAbyss.CameraSystem
{
    /// <summary>镜头请求的所有权句柄。释放是幂等操作。</summary>
    public sealed class CameraShotLease : IDisposable
    {
        private CameraDirector _director;
        private readonly int _requestId;

        internal CameraShotLease(CameraDirector director, int requestId)
        {
            _director = director;
            _requestId = requestId;
        }

        public bool IsValid => _director != null && _director.ContainsRequest(_requestId);

        public void Dispose()
        {
            if (_director == null)
                return;

            CameraDirector director = _director;
            _director = null;
            director.ReleaseShot(_requestId);
        }
    }
}
