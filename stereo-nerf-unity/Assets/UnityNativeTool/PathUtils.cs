using System.IO;

namespace UnityNativeTool.Internal
{
    public static class PathUtils
    {
        /// <summary>
        /// Compares paths to dll file returned from <see cref="UnityEditor.Compilation.Assembly.outputPath"/> and <see cref="System.Reflection.Assembly.Location"/>
        /// </summary>
        /// <param name="outputPath">Value returned from <see cref="UnityEditor.Compilation.Assembly.outputPath"/></param>
        /// <param name="location">Value returned from <see cref="System.Reflection.Assembly.Location"/></param>
        public static bool DllPathsEqual(string outputPath, string location)
        {
            return NormallizeUnityAssemblyPath(outputPath) == NormallizeSystemAssemblyPath(location);
        }

        /// <summary>
        /// Normalizes path returned from <see cref="UnityEditor.Compilation.Assembly.outputPath"/>
        /// </summary>
        public static string NormallizeUnityAssemblyPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        /// <summary>
        /// Normalizes path returned from <see cref="System.Reflection.Assembly.Location"/>
        /// </summary>
        public static string NormallizeSystemAssemblyPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
