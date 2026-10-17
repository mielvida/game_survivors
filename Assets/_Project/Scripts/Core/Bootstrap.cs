using UnityEngine;

namespace Survivors
{
    // Builds the whole prototype scene at runtime, so any empty scene can be played.
    public static class Bootstrap
    {
        public static GameObject Root { get; private set; }

        static BootstrapRunner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            var host = new GameObject("BootstrapRunner");
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<BootstrapRunner>();
            Build();
        }

        public static void RequestRestart()
        {
            if (_runner != null) _runner.Pending = true;
        }

        internal static void Rebuild()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            Visuals.ClearCache();
            Build();
        }

        static void Build()
        {
            Time.timeScale = 1f;
            Root = new GameObject("Run");

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                if (Object.FindFirstObjectByType<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            }

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Sun");
                lightGo.transform.SetParent(Root.transform, false);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                l.color = new Color(1f, 0.96f, 0.88f);
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.55f);

            var ground = Root.AddComponent<GroundFollower>();
            ground.Build();

            var heroGo = new GameObject("Hero");
            heroGo.transform.SetParent(Root.transform, false);
            var hero = heroGo.AddComponent<Hero>();
            hero.Build();

            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(Root.transform, false);
            enemies.AddComponent<EnemyManager>();

            var gems = new GameObject("Gems");
            gems.transform.SetParent(Root.transform, false);
            gems.AddComponent<GemManager>();

            var run = Root.AddComponent<RunController>();
            run.Init(hero);

            var rig = Root.AddComponent<CameraRig>();
            rig.Setup(cam);

            Root.AddComponent<RunHud>();
        }
    }

    public sealed class BootstrapRunner : MonoBehaviour
    {
        public bool Pending;

        void Update()
        {
            if (!Pending) return;
            Pending = false;
            Bootstrap.Rebuild();
        }
    }

    // Large checkerboard plane that snaps to the hero in whole texture periods,
    // so it looks infinite and the pattern stays fixed in world space.
    public sealed class GroundFollower : MonoBehaviour
    {
        const float PlaneSize = 176f;  // metres (a Unity Plane is 10 units at scale 1)
        const float Period = 8f;       // metres per full 2x2 checker repeat

        Transform _plane;

        public void Build()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var col = g.GetComponent<Collider>();
            if (col != null) Destroy(col);
            g.name = "Ground";
            g.transform.SetParent(transform, false);
            g.transform.localScale = new Vector3(PlaneSize / 10f, 1f, PlaneSize / 10f);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            var a = new Color(0.16f, 0.27f, 0.22f);
            var b = new Color(0.19f, 0.31f, 0.25f);
            tex.SetPixels(new[] { a, b, b, a });
            tex.Apply();

            var mat = new Material(Visuals.Mat(a));
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(PlaneSize / Period, PlaneSize / Period);
            g.GetComponent<Renderer>().sharedMaterial = mat;
            _plane = g.transform;
        }

        void LateUpdate()
        {
            var h = Hero.I;
            if (h == null || _plane == null) return;
            _plane.position = new Vector3(
                Mathf.Round(h.X / Period) * Period,
                0f,
                Mathf.Round(h.Z / Period) * Period);
        }
    }
}
