using System.IO;
using Unity.Build;

namespace Unity.Platforms.iOS.Build
{
    sealed class iOSBuildArtifact : IBuildArtifact
    {
        public FileInfo OutputTargetFile;
    }
}
