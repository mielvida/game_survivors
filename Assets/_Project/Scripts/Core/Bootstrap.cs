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

            ArenaBuilder.Build(Root.transform);

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

    // Static square arena: checkerboard floor, low stone walls and corner pillars.
    public static class ArenaBuilder
    {
        const float Period = 8f; // metres per full 2x2 checker repeat

        public static void Build(Transform parent)
        {
            float half = Config.ArenaHalf;
            float size = half * 2f;

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var col = g.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            g.name = "Ground";
            g.transform.SetParent(parent, false);
            g.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            var a = new Color(0.16f, 0.27f, 0.22f);
            var b = new Color(0.19f, 0.31f, 0.25f);
            tex.SetPixels(new[] { a, b, b, a });
            tex.Apply();

            var mat = new Material(Visuals.Mat(a));
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(size / Period, size / Period);
            g.GetComponent<Renderer>().sharedMaterial = mat;

            var wall = new Color(0.49f, 0.51f, 0.55f);
            float len = size + 3f;
            Wall(parent, wall, new Vector3(0f, 0.35f, -half - 0.5f), new Vector3(len, 0.7f, 1f));
            Wall(parent, wall, new Vector3(0f, 0.35f, half + 0.5f), new Vector3(len, 0.7f, 1f));
            Wall(parent, wall, new Vector3(-half - 0.5f, 0.35f, 0f), new Vector3(1f, 0.7f, len));
            Wall(parent, wall, new Vector3(half + 0.5f, 0.35f, 0f), new Vector3(1f, 0.7f, len));

            var pillar = new Color(0.36f, 0.39f, 0.42f);
            var torch = new Color(1f, 0.65f, 0.24f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float cx = sx * (half + 0.5f), cz = sz * (half + 0.5f);
                    Wall(parent, pillar, new Vector3(cx, 1.8f, cz), new Vector3(2f, 3.6f, 2f));
                    var orb = Visuals.Prim(PrimitiveType.Sphere, torch, new Vector3(1f, 1f, 1f), parent);
                    orb.transform.position = new Vector3(cx, 4.3f, cz);
                }
        }

        static void Wall(Transform parent, Color c, Vector3 pos, Vector3 scale)
        {
            var w = Visuals.Prim(PrimitiveType.Cube, c, scale, parent);
            w.transform.position = pos;
        }
    }
}
