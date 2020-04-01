using Unity.Build;

namespace Unity.Platforms.iOS.Build
{
    sealed class iOSRunInstance : IRunInstance
    {
        public bool IsRunning => true;

        public iOSRunInstance() { }

        public void Dispose() { }
    }
}
