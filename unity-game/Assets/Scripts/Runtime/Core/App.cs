using TFY.CatAdventure.Audio;
using TFY.CatAdventure.Backend;
using TFY.CatAdventure.Data;
using TFY.CatAdventure.Gameplay;
using TFY.CatAdventure.Graphics;
using TFY.CatAdventure.UI;
using TFY.CatAdventure.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TFY.CatAdventure
{
    /// <summary>
    /// Composition root. The Boot scene only needs this one component (TFY ▸ Setup creates it); if a scene is empty, it creates
    /// itself. Everything else — services, camera, lights, world, cat, UI — is built here in code, so nothing can be mis-wired.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public ContentService Content { get; private set; }
        public SaveService Save { get; private set; }
        public BackendService Backend { get; private set; }
        public AudioService Audio { get; private set; }
        public QualityService Quality { get; private set; }
        public PostFX Post { get; private set; }
        public SkyLighting Sky { get; private set; }
        public EnvironmentBuilder Environment { get; private set; }
        public RunController Run { get; private set; }
        public CatController Cat { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public UIManager UI { get; private set; }
        public MenuStage Stage { get; private set; }
        public AppFlow Flow { get; private set; }
        public InputController Input { get; private set; }
        private ReflectionProbe probe; private float probeTimer;
        private Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (!FindFirstObjectByType<App>()) new GameObject("TFY Cat Adventure").AddComponent<App>();
        }

        private void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this; DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            MaterialLibrary.Init();

            Content = new ContentService();
            Content.LoadBundledAndCached(); // bundled defaults + last online copy: the game can always start offline
            Save = new SaveService(Content);
            IBackend backend =
#if TFY_FIREBASE
                new FirebaseBackend();
#else
                new OfflineBackend();
#endif
            Backend = new BackendService(backend, Content, Save);

            Audio = Child<AudioService>("Audio");
            UIKit.Audio = Audio;
            Quality = Child<QualityService>("Quality");

            // Camera with URP post-processing.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" }; camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = 0.2f; cam.farClipPlane = 450f;
            cam.clearFlags = CameraClearFlags.Skybox; camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>(); camData.renderPostProcessing = true;
            CameraRig = camGo.AddComponent<CameraRig>(); CameraRig.cam = cam;
            Post = new PostFX(transform);

            var world = new GameObject("World").transform; world.SetParent(transform, false);
            Environment = new EnvironmentBuilder(world);
            var trackRoot = new GameObject("Track").transform; trackRoot.SetParent(transform, false);

            // The cat.
            var catGo = new GameObject("Player"); catGo.transform.SetParent(trackRoot, false);
            var anim = catGo.AddComponent<CatAnimator>();
            Cat = catGo.AddComponent<CatController>(); Cat.anim = anim;
            CameraRig.target = catGo.transform;
            Sky = new SkyLighting(transform, catGo.transform, 6);

            var obstacles = new ObstacleFactory(Group(trackRoot, "Obstacles"));
            var collectibles = new CollectibleFactory(Group(trackRoot, "Collectibles"), Content);
            Run = Child<RunController>("Run", false);
            Run.Cat = Cat; Run.Camera = CameraRig;
            Run.Track = new TrackBuilder(obstacles, collectibles, Group(trackRoot, "Markers"), Content);
            Run.enabled = true;

            Input = Child<InputController>("Input");
            Input.OnInput += i => Run.Input(i);

            UI = Child<UIManager>("UI");
            Stage = new MenuStage(this);
            Flow = new AppFlow(this);
            Quality.Changed += OnQualityChanged;
            GameEvents.Landed += () => { if (Save.Player.settings.vibration) Haptics.Tap(10); };
        }

        private async void Start()
        {
            UI.Init();
            await Flow.Boot();
        }

        private T Child<T>(string name, bool enabled = true) where T : Component
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); var c = go.AddComponent<T>();
            if (c is Behaviour b) b.enabled = enabled; return c;
        }

        private static Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        private void OnQualityChanged(QualityTier tier)
        {
            var b = Quality.Current;
            Post.ApplyTier(b, Save.Player.settings.reduceMotion);
            Environment.Quality = b.scenery;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            data.antialiasing = tier >= QualityTier.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : tier == QualityTier.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            data.renderShadows = b.shadowDistance > 0;
            if (b.reflections && !probe)
            {
                probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>(); probe.transform.SetParent(cam.transform, false);
                probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces; probe.size = new Vector3(200, 80, 200); probe.resolution = tier == QualityTier.Ultra ? 256 : 128;
            }
            if (probe) probe.enabled = b.reflections;
        }

        private void Update()
        {
            // One real-time reflection probe, re-rendered in slices every few seconds (HIGH/ULTRA): shiny coins, water, cars.
            if (probe && probe.enabled && (probeTimer -= Time.unscaledDeltaTime) <= 0) { probeTimer = 3f; probe.RenderProbe(); }
        }

        private void OnApplicationPause(bool paused) { if (paused) { Flow?.Pause(); Save?.Save(); } }
        private void OnApplicationQuit() { Save?.Save(); }
    }
}
