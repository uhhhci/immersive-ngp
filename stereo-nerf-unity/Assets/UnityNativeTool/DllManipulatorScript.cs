using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityNativeTool.Internal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityNativeTool
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    #endif
    public class DllManipulatorScript : MonoBehaviour
    {
        private static DllManipulatorScript _singletonInstance = null;
        public TimeSpan? InitializationTime { get; private set; } = null;
        public DllManipulatorOptions Options = new DllManipulatorOptions()
        {
            dllPathPattern =
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            "{assets}/Plugins/__{name}.so",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            "{assets}/Plugins/__{name}.dylib",
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            "{assets}/Plugins/__{name}.dll",
#else
            "",
#endif
            assemblyNames = new List<string>(),
            loadingMode = DllLoadingMode.Lazy,
            posixDlopenFlags = PosixDlopenFlags.Lazy,
            threadSafe = false,
            enableCrashLogs = false,
            crashLogsDir = "{assets}/",
            crashLogsStackTrace = false,
            mockAllNativeFunctions = true,
            onlyInEditor = true,
            enableInEditMode = false
        };
        
        public static ConcurrentQueue<Action> MainThreadTriggerQueue = new ConcurrentQueue<Action>();

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (_singletonInstance != null)
            {
                if (EditorApplication.isPlaying)
                    Destroy(gameObject);
                else if(_singletonInstance != this)
                    enabled = false; //Don't destroy as the user may be editing a Prefab
                return;
            }
            _singletonInstance = this;
            
            if(EditorApplication.isPlaying)
                DontDestroyOnLoad(gameObject);

            if(EditorApplication.isPlaying || Options.enableInEditMode)
            {
                Initialize();
                AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            }

            // Ensure update is called every frame in edit mode, ExecuteInEditMode only calls Update when the scene changes
            if(!EditorApplication.isPlaying && Options.enableInEditMode)
                EditorApplication.update += Update;

#else
            if (Options.onlyInEditor) 
                return;

            if (_singletonInstance != null)
            {
                Destroy(gameObject);
                return;
            }
            _singletonInstance = this;

            DontDestroyOnLoad(gameObject);
            Initialize();
#endif
        }
        
        public void Initialize()
        {
            var initTimer = System.Diagnostics.Stopwatch.StartNew();

            DllManipulator.Initialize(Options, Thread.CurrentThread.ManagedThreadId, Application.dataPath);

            initTimer.Stop();
            InitializationTime = initTimer.Elapsed;
        }

        /// <summary>
        /// Will reset the DllManipulator and Initialize it again.
        /// Note: Unloads all Dlls, may be a dangerous operation if using preloaded
        /// </summary>
        public void Reinitialize()
        {
            DllManipulator.Reset();
            
#if UNITY_EDITOR
            if(EditorApplication.isPlaying || Options.enableInEditMode)
#endif
                Initialize();
        }
        
        /// <summary>
        /// Note: also called in edit mode if Options.enableInEditMode is set.
        /// </summary>
        private void Update()
        {
            InvokeMainThreadQueue();
        }

        /// <summary>
        /// Executes queued methods.
        /// Should be called from the main thread in Update.
        /// </summary>
        public static void InvokeMainThreadQueue()
        {
            while (MainThreadTriggerQueue.TryDequeue(out var action))
                action();
        }

#if UNITY_EDITOR
        private bool _isRecompiling;
        /// <summary>
        /// Called when Assemblies are reloaded due to recompilation.
        /// Called before OnDisable.
        /// </summary>
        private void OnBeforeAssemblyReload()
        {
            _isRecompiling = true;
        }
        
        private void OnDisable()
        {
            if(_singletonInstance == this && !EditorApplication.isPlaying && Options.enableInEditMode)
            {
                EditorApplication.update -= Update;
                AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
                
                // When recompiling OnDestroy is not called by default (the object is not really destroyed)
                // Manually trigger OnDestroy to clean up if we are disabled because of recompilation
                if (_isRecompiling)
                {
                    _isRecompiling = false;
                    Reset();
                }
            }
        }
#endif

        private void OnDestroy()
        {
            if (_singletonInstance == this)
                Reset();
        }

        private void Reset()
        {
            //Note on threading: Because we don't wait for other threads to finish, we might be stealing function delegates from under their nose if Unity doesn't happen to close them yet.
            //On Preloaded mode this leads to NullReferenceException, but on Lazy mode the DLL and function would be just reloaded so we would up with loaded DLL after game exit.
            //Thankfully thread safety with Lazy mode is not implemented yet.

            if (DllManipulator.Options != null) // Check that we have initialized
                DllManipulator.Reset();
            _singletonInstance = null;
        }
    }
}