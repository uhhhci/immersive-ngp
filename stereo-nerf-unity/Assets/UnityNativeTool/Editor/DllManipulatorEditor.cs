using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.ShortcutManagement;
#endif
using System.IO;
using System;

namespace UnityNativeTool.Internal
{
    [CustomEditor(typeof(DllManipulatorScript))]
    public class DllManipulatorEditor : Editor
    {
        private static readonly string INFO_BOX_GUI_CONTENT = 
            "Mocks native functions to allow manually un/loading native DLLs. DLLs are always unloaded at OnDestroy. Configuration changes below are always applied at OnEnable.";
        private static readonly GUIContent TARGET_ALL_NATIVE_FUNCTIONS_GUI_CONTENT = new GUIContent("All native functions",
            "If true, all found native functions will be mocked.\n\n" +
            $"If false, you have to select them by using [{nameof(MockNativeDeclarationsAttribute)}] or [{nameof(MockNativeDeclarationAttribute)}].");
        private static readonly GUIContent ONLY_ASSEMBLY_CSHARP_GUI_CONTENT = new GUIContent("Only Assembly-CSharp(-Editor)",
            "If true, native functions will be mocked only in Assembly-CSharp and Assembly-CSharp-Editor. Alternatively enter a list of assembly names.");
        private static readonly GUIContent ONLY_IN_EDITOR = new GUIContent("Only in Editor",
            "Whether to run only inside editor (which is recommended).");
        private static readonly GUIContent ENABLE_IN_EDIT_MODE = new GUIContent("Enable in Edit Mode",
            "Should the DLLs also be mocked in edit mode (i.e. even if you don't hit 'play' in editor). " +
            "Turning this off when not needed improves performance when entering edit mode. " +
            "Changes are currently only visible on the next time edit mode is entered (i.e. when OnEnable is called so hit 'play' then 'stop' to apply).");
        private static readonly GUIContent TARGET_ASSEMBLIES_GUI_CONTENT = new GUIContent("Target assemblies",
            "List of assembly names to mock native functions in (no file extension).");
        private static readonly GUIContent DLL_PATH_PATTERN_GUI_CONTENT = new GUIContent("DLL path pattern", 
            "Available macros:\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_DLL_NAME_MACRO} - name of DLL as specified in [DllImport] attribute.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_ASSETS_MACRO} - assets folder of current project.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_PROJECT_MACRO} - project folder i.e. one above Assets.");
        private static readonly GUIContent DLL_LOADING_MODE_GUI_CONTENT = new GUIContent("DLL loading mode", 
            "Specifies how DLLs and functions will be loaded.\n\n" +
            "Lazy - All DLLs and functions are loaded each time they are called, if not loaded yet. This allows them to be easily unloaded and loaded within game execution.\n\n" +
            "Preloaded - Slight performance benefit over Lazy mode. All declared DLLs and functions are loaded at startup (OnEnable()) and not reloaded later. Mid-execution it's not safe to unload them unless game is paused.");
        private static readonly GUIContent POSIX_DLOPEN_FLAGS_GUI_CONTENT = new GUIContent("dlopen flags",
            "Flags used in dlopen() P/Invoke on Linux and OSX systems. Has minor meaning unless library is large.");
        private static readonly GUIContent THREAD_SAFE_GUI_CONTENT = new GUIContent("Thread safe",
            "Ensures synchronization required for native calls from any other than Unity main thread. Overhead might be few times higher, with uncontended locks.\n\n" +
            "Only in Preloaded mode.");
        private static readonly GUIContent CRASH_LOGS_GUI_CONTENT = new GUIContent("Crash logs",
            "Logs each native call to file. In case of crash or hang caused by native function, you can than see what function was that, along with arguments and, optionally, stack trace.\n\n" +
            "In multi-threaded scenario there will be one file for each thread and you'll have to guess the right one (call index will be a hint).\n\n" +
            "Note that existence of log files doesn't mean the crash was caused by any tracked native function.\n\n" +
            "Overhead is HIGH (on poor PC there might be just few native calls per update to disturb 60 fps.)");
        private static readonly GUIContent CRASH_LOGS_DIR_GUI_CONTENT = new GUIContent("Logs directory",
            "Path to directory in which crash logs will be stored. You can use macros as in DLL path. Note that this file(s) will usually exist during majority of game execution.");
        private static readonly GUIContent CRASH_LOGS_STACK_TRACE_GUI_CONTENT = new GUIContent("Stack trace",
            "Whether to include stack trace in crash log.\n\n" +
            "Overhead is about 4 times higher.");
        private static readonly GUIContent UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT = new GUIContent("Unload all DLLs [dangerous]",
            "Use only if you are sure no mocked native calls will be made while DLL is unloaded.");
        private static readonly GUIContent UNLOAD_ALL_DLLS_WITH_THREAD_SAFETY_GUI_CONTENT = new GUIContent("Unload all DLLs [dangerous]",
            "Use only if you are sure no other thread will call mocked natives.");
        private static readonly GUIContent UNLOAD_ALL_DLLS_AND_PAUSE_WITH_THREAD_SAFETY_GUI_CONTENT = new GUIContent("Unload all DLLs & Pause [dangerous]",
            "Use only if you are sure no other thread will call mocked natives.");
        private static readonly TimeSpan ASSEMBLIES_REFRESH_INTERVAL = TimeSpan.FromSeconds(5);
        
        private static readonly GUIContent INITIALIZE_ENABLED_EDIT_MODE_GUI_CONTENT = new GUIContent(
                "Apply Changes Now & Initialize",
                "Start mocking native functions in edit mode immediately without waiting for OnEnable.");
        private static readonly GUIContent REINITIALIZE_WITH_CHANGES_LAZY_GUI_CONTENT = new GUIContent(
            "Unload, Apply Changes Now & Reinitialize",
            "Changes made to the options above are only applied when play(/edit) mode is entered." +
            " Use this to unload all Dlls and initialize with the new changes immediately.");
        private static readonly GUIContent REINITIALIZE_WITH_CHANGES_PRELOADED_GUI_CONTENT = new GUIContent(
            "Unload, Apply Changes Now & Reinitialize [Dangerous]",
            "Changes made to the options above are only applied when play(/edit) mode is entered. " +
            "Use this to unload all Dlls and initialize with the new changes immediately. " +
            "Use only if you are sure no mocked native calls will be made while DLL is unloaded.");

        private bool _showLoadedLibraries = true;
        private bool _showTargetAssemblies = true;
        private string[] _possibleTargetAssemblies = null;
        private DateTime _lastKnownAssembliesRefreshTime;

        /// <summary>
        /// Used to check if the options have changed, in order to set the object as dirty so changes are saved
        /// </summary>
        private DllManipulatorOptions _prevOptions = new DllManipulatorOptions();
        
        public static event Action RepaintAllEditors = delegate {};
        
        public DllManipulatorEditor()
        {
            EditorApplication.pauseStateChanged += _ => Repaint();
            EditorApplication.playModeStateChanged += _ => Repaint();
            RepaintAllEditors += Repaint;
        }
        
        private void Awake()
        {
            // Immediately copy the Options to the previous so we don't need to check for null later
            ((DllManipulatorScript)target).Options.CloneTo(_prevOptions);
        }

        public override void OnInspectorGUI()
        {
            var t = (DllManipulatorScript)this.target;

            EditorGUILayout.HelpBox(INFO_BOX_GUI_CONTENT, MessageType.Info);

            DrawOptions(t.Options);

            DetectOptionChanges(t);
            
            EditorGUILayout.Space();

            DrawCurrentState(t);
        }

        /// <summary>
        /// Detects whether the <see cref="DllManipulatorScript.Options"/> have changed, both relative to the previous
        /// options and the <see cref="DllManipulator.Options"/> if we are currently initialized.
        /// </summary>
        /// <param name="t">The OnInspectorGUI target</param>
        private void DetectOptionChanges(DllManipulatorScript t)
        {
            // Set the target as dirty so changes can be saved, if there are changes
            if (GUI.changed)
            {
                if (!t.Options.Equals(_prevOptions))
                {
                    // If the options have changed then update the _prevOptions and notify there are changes to be saved
                    // CloneTo is used to ensure a deep copy is made
                    t.Options.CloneTo(_prevOptions);
                    EditorUtility.SetDirty(target);
                }
            }

            // Allow Reinitializing DllManipulator if there are changes
            if (DllManipulator.Options != null && !t.Options.Equals(DllManipulator.Options))
            {
                if (DllManipulator.Options.loadingMode == DllLoadingMode.Preload)
                {
                    if (GUILayout.Button(REINITIALIZE_WITH_CHANGES_PRELOADED_GUI_CONTENT))
                        t.Reinitialize();
                }
                else if(GUILayout.Button(REINITIALIZE_WITH_CHANGES_LAZY_GUI_CONTENT))
                {
                    t.Reinitialize();
                }
            }
            
            // When enabling enableInEditMode for the first time, allow immediately initializing without waiting for OnEnable
            if(DllManipulator.Options == null && t.Options.enableInEditMode && !EditorApplication.isPlaying && 
               GUILayout.Button(INITIALIZE_ENABLED_EDIT_MODE_GUI_CONTENT))
            {
                t.Initialize();
            }
        }

        /// <summary>
        /// Draws GUI related to the current state of the DllManipulator.
        /// Buttons to load/unload Dlls as well as details about which Dlls are loaded
        /// </summary>
        /// <param name="t">The OnInspectorGUI target</param>
        private void DrawCurrentState(DllManipulatorScript t)
        {
            if (DllManipulator.Options == null) // Exit if we have not initialized DllManipulator
                return;
            
            var usedDlls = DllManipulator.GetUsedDllsInfos();
            if (usedDlls.Count != 0)
            {
                if(DllManipulator.Options.loadingMode == DllLoadingMode.Preload && usedDlls.Any(d => !d.isLoaded))
                {
                    if (EditorApplication.isPaused)
                    {
                        if (GUILayout.Button("Load all DLLs & Unpause"))
                        {
                            DllManipulator.LoadAll();
                            EditorApplication.isPaused = false;
                        }
                    }

                    if (GUILayout.Button("Load all DLLs"))
                        DllManipulator.LoadAll();
                }

                if (usedDlls.Any(d => d.isLoaded))
                {
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    {
                        bool pauseAndUnloadAll;
                        if(DllManipulator.Options.threadSafe)
                            pauseAndUnloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_AND_PAUSE_WITH_THREAD_SAFETY_GUI_CONTENT);
                        else
                            pauseAndUnloadAll = GUILayout.Button("Unload all DLLs & Pause");

                        if(pauseAndUnloadAll)
                        {
                            EditorApplication.isPaused = true;
                            DllManipulator.UnloadAll();
                        }
                    }


                    bool unloadAll;
                    if(DllManipulator.Options.threadSafe)
                        unloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_WITH_THREAD_SAFETY_GUI_CONTENT);
                    else if (DllManipulator.Options.loadingMode == DllLoadingMode.Preload && (EditorApplication.isPlaying && !EditorApplication.isPaused || DllManipulator.Options.enableInEditMode))
                        unloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT);
                    else
                        unloadAll = GUILayout.Button("Unload all DLLs");

                    if(unloadAll)
                        DllManipulator.UnloadAll();
                }

                DrawUsedDlls(usedDlls);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No DLLs to mock");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (t.InitializationTime != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                var time = t.InitializationTime.Value;
                EditorGUILayout.LabelField($"Initialized in: {(int)time.TotalSeconds}.{time.Milliseconds.ToString("D3")}s");
            }
        }

        private void DrawUsedDlls(IList<NativeDllInfo> usedDlls)
        {
            _showLoadedLibraries = EditorGUILayout.Foldout(_showLoadedLibraries, "Mocked DLLs");
            if (_showLoadedLibraries)
            {
                var prevIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel += 1;
                bool isFirstDll = true;
                foreach (var dll in usedDlls)
                {
                    if (!isFirstDll)
                        EditorGUILayout.Space();

                    var stateAttributes = new List<string>
                        {
                            dll.isLoaded ? "LOADED" : "NOT LOADED"
                        };
                    if (dll.loadingError)
                        stateAttributes.Add("LOAD ERROR");
                    if (dll.symbolError)
                        stateAttributes.Add("SYMBOL ERRROR");
                    var state = string.Join(" | ", stateAttributes);

                    EditorGUILayout.LabelField($"[{state}] {dll.name}");
                    EditorGUILayout.LabelField(dll.path);
                    isFirstDll = false;
                }
                EditorGUI.indentLevel = prevIndent;
            }
        }

        private void DrawOptions(DllManipulatorOptions options)
        {
            options.onlyInEditor = EditorGUILayout.Toggle(ONLY_IN_EDITOR, options.onlyInEditor);
            options.enableInEditMode = EditorGUILayout.Toggle(ENABLE_IN_EDIT_MODE, options.enableInEditMode);

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Managed Side", EditorStyles.boldLabel);

            options.mockAllNativeFunctions = EditorGUILayout.Toggle(TARGET_ALL_NATIVE_FUNCTIONS_GUI_CONTENT, options.mockAllNativeFunctions);

            if (EditorGUILayout.Toggle(ONLY_ASSEMBLY_CSHARP_GUI_CONTENT, options.assemblyNames.Count == 0))
            {
                options.assemblyNames.Clear();
            }
            else
            {
                var prevIndent1 = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                if (_possibleTargetAssemblies == null || _lastKnownAssembliesRefreshTime + ASSEMBLIES_REFRESH_INTERVAL < DateTime.Now)
                    RefreshPossibleTargetAssemblies();
                
                if (options.assemblyNames.Count == 0)
                    options.assemblyNames.AddRange(DllManipulator.DEFAULT_ASSEMBLY_NAMES);

                _showTargetAssemblies = EditorGUILayout.Foldout(_showTargetAssemblies, TARGET_ASSEMBLIES_GUI_CONTENT);
                if (_showTargetAssemblies)
                {
                    var prevIndent2 = EditorGUI.indentLevel;
                    EditorGUI.indentLevel++;

                    DrawList(options.assemblyNames, i =>
                        {
                            var result = EditorGUILayout.TextField(options.assemblyNames[i]);

                            // Show a pop up for quickly selecting an assembly
                            var selectedId = EditorGUILayout.Popup(0,
                                new[] {"Find"}.Concat(_possibleTargetAssemblies).ToArray(), GUILayout.Width(80));

                            if (selectedId > 0)
                                result = _possibleTargetAssemblies[selectedId - 1];
                            return result;
                        }, true, () => "",
                        () =>
                        {
                            options.assemblyNames = options.assemblyNames
                                .Concat(_possibleTargetAssemblies).Distinct().ToList();
                        });

                    EditorGUI.indentLevel = prevIndent2;
                }

                EditorGUI.indentLevel = prevIndent1;
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Native Side", EditorStyles.boldLabel);

            options.dllPathPattern = EditorGUILayout.TextField(DLL_PATH_PATTERN_GUI_CONTENT, options.dllPathPattern);
            
            options.loadingMode = (DllLoadingMode)EditorGUILayout.EnumPopup(DLL_LOADING_MODE_GUI_CONTENT, options.loadingMode);

#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            options.posixDlopenFlags = (PosixDlopenFlags)EditorGUILayout.EnumPopup(POSIX_DLOPEN_FLAGS_GUI_CONTENT, options.posixDlopenFlags);
#endif

            var guiEnabled = GUI.enabled;
            if (options.loadingMode != DllLoadingMode.Preload)
            {
                options.threadSafe = false;
                GUI.enabled = false;
            }
            options.threadSafe = EditorGUILayout.Toggle(THREAD_SAFE_GUI_CONTENT, options.threadSafe);
            GUI.enabled = guiEnabled;

            options.enableCrashLogs = EditorGUILayout.Toggle(CRASH_LOGS_GUI_CONTENT, options.enableCrashLogs);

            if (options.enableCrashLogs)
            {
                var prevIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel += 1;

                options.crashLogsDir = EditorGUILayout.TextField(CRASH_LOGS_DIR_GUI_CONTENT, options.crashLogsDir);

                options.crashLogsStackTrace = EditorGUILayout.Toggle(CRASH_LOGS_STACK_TRACE_GUI_CONTENT, options.crashLogsStackTrace);

                EditorGUI.indentLevel = prevIndent;
            }
        }

        /// <summary>
        /// Will search for all managed assemblies and store them in <see cref="_possibleTargetAssemblies"/>.
        /// Excludes assemblies starting with <see cref="DllManipulator.IGNORED_ASSEMBLY_PREFIXES"/>
        /// </summary>
        private void RefreshPossibleTargetAssemblies()
        {
            var playerCompiledAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .Select(a => Path.GetFileNameWithoutExtension(a.outputPath));

            var editorCompiledAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Select(a => Path.GetFileNameWithoutExtension(a.outputPath));

            var assemblyAssets = Resources.FindObjectsOfTypeAll<PluginImporter>()
                .Where(p => !p.isNativePlugin)
                .Select(p => Path.GetFileNameWithoutExtension(p.assetPath));

            _possibleTargetAssemblies = playerCompiledAssemblies
                .Concat(editorCompiledAssemblies)
                .Concat(assemblyAssets)
                .Where(a => !DllManipulator.IGNORED_ASSEMBLY_PREFIXES.Any(a.StartsWith))
                .OrderBy(name => name)
                .ToArray();
            _lastKnownAssembliesRefreshTime = DateTime.Now;
        }

        private void DrawList<T>(IList<T> elements, Func<int, T> drawElement, bool canAddNewElement, Func<T> getNewElement, Action addAll)
        {
            int indexToRemove = -1;
            for (int i = 0; i < elements.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                elements[i] = drawElement(i);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    indexToRemove = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (indexToRemove != -1)
                elements.RemoveAt(indexToRemove);

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            var prevGuiEnabled = GUI.enabled;
            GUI.enabled = prevGuiEnabled && canAddNewElement;
            
            if (GUILayout.Button("Add", GUILayout.Width(40)))
                elements.Add(getNewElement());

            if (GUILayout.Button("Add All", GUILayout.Width(80)))
                addAll();
            
            if (GUILayout.Button("Reset", GUILayout.Width(50)))
                elements.Clear();

            GUI.enabled = prevGuiEnabled;
            GUILayout.EndHorizontal();
        }

        static string GetFirstAssemblyToList(string[] allAssemblies)
        {
            return allAssemblies.FirstOrDefault(a => PathUtils.DllPathsEqual(a, typeof(DllManipulator).Assembly.Location)) 
                ?? allAssemblies.FirstOrDefault();
        }

        #if UNITY_2019_1_OR_NEWER
        [Shortcut("Tools/Load All Dlls", KeyCode.D, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        #else
        [MenuItem("Tools/Load All Dlls #&d")]
        #endif
        public static void LoadAllShortcut()
        {
            DllManipulator.LoadAll();
        }

        #if UNITY_2019_1_OR_NEWER
        [Shortcut("Tools/Unload All Dlls", KeyCode.D, ShortcutModifiers.Alt)]
        #else
        [MenuItem("Tools/Unload All Dlls &d")]
        #endif
        public static void UnloadAll()
        {
            DllManipulator.UnloadAll();
        }

        [NativeDllLoadedTrigger(UseMainThreadQueue = true)]
        [NativeDllAfterUnloadTrigger(UseMainThreadQueue = true)]
        public static void RepaintAll()
        {
            RepaintAllEditors?.Invoke();
        }
    }
}
