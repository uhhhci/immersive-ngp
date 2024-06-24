using UnityEngine;
using UnityEditor;

// Ensure class initializer is called whenever scripts recompile
//[InitializeOnLoad]
public class StereoNeRFPluginEditorManager
{
    private const int DEINIT_VULKAN = 0x0005;

    static void CleanupOnQuit()
    {
        GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), DEINIT_VULKAN);
    }

    //static void OnEditorQuit()
    //{
    //    EditorApplication.quitting += CleanupOnQuit;
    //}
}