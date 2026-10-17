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

    // Static square arena: checkerboard floor surrounded by a rough stone wall.
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

            // Stone wall: dark mortar core, three rows of staggered blocks, corner towers with torches.
            var rng = new System.Random(7);
            var stone = new[]
            {
                new Color(0.47f, 0.45f, 0.44f), new Color(0.52f, 0.51f, 0.48f), new Color(0.42f, 0.41f, 0.38f),
                new Color(0.55f, 0.54f, 0.51f), new Color(0.44f, 0.42f, 0.40f)
            };
            var moss = new Color(0.29f, 0.54f, 0.30f);
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = (side % 2 == 0) ? -1f : 1f;
                float hs = (side == 1) ? 0.6f : 1f; // the near wall stays lower so the hero is not hidden
                float edge = sign * (half + 0.55f);
                float total = size + 4f;

                var core = Visuals.Prim(PrimitiveType.Cube, new Color(0.18f, 0.17f, 0.16f), alongX ? new Vector3(total, 1.9f * hs, 1f) : new Vector3(1f, 1.9f * hs, total), parent);
                core.transform.position = alongX ? new Vector3(0f, 0.95f * hs, edge) : new Vector3(edge, 0.95f * hs, 0f);

                for (int row = 0; row < 3; row++)
                {
                    float k = -half - 2f + (row % 2) * 0.8f;
                    while (k < half + 2f)
                    {
                        float len = 1.2f + (float)rng.NextDouble() * 1.3f;
                        float bh = (0.58f + (float)rng.NextDouble() * 0.12f) * hs;
                        float depth = 1.05f + (float)rng.NextDouble() * 0.3f;
                        float yc = (row * 0.64f + 0.32f) * hs;
                        float along = k + len / 2f;
                        var blk = Visuals.Prim(PrimitiveType.Cube, stone[rng.Next(stone.Length)],
                            alongX ? new Vector3(len - 0.07f, bh - 0.05f, depth) : new Vector3(depth, bh - 0.05f, len - 0.07f), parent);
                        blk.transform.position = alongX ? new Vector3(along, yc, edge) : new Vector3(edge, yc, along);
                        blk.transform.rotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 3f, 0f);
                        if (row == 2 && rng.NextDouble() < 0.5)
                        {
                            var m = Visuals.Prim(PrimitiveType.Sphere, moss, new Vector3(0.9f, 0.25f, 0.7f), parent);
                            m.transform.position = blk.transform.position + new Vector3(0f, bh * 0.5f, 0f);
                        }
                        k += len;
                    }
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float cx = sx * (half + 0.6f), cz = sz * (half + 0.6f);
                    for (int t = 0; t < 4; t++)
                    {
                        float w = 2.3f - t * 0.12f;
                        var tb = Visuals.Prim(PrimitiveType.Cube, stone[(t + 2) % stone.Length], new Vector3(w, 0.6f, w), parent);
                        tb.transform.position = new Vector3(cx, 0.45f + t * 0.62f, cz);
                        tb.transform.rotation = Quaternion.Euler(0f, t * 7f, 0f);
                    }
                    var cap = Visuals.Prim(PrimitiveType.Cube, new Color(0.29f, 0.28f, 0.26f), new Vector3(2f, 0.3f, 2f), parent);
                    cap.transform.position = new Vector3(cx, 3.0f, cz);
                    var flame = Visuals.Prim(PrimitiveType.Sphere, new Color(1f, 0.65f, 0.24f), new Vector3(0.7f, 0.9f, 0.7f), parent);
                    flame.transform.position = new Vector3(cx, 3.7f, cz);
                }

            // Dark forest floor beyond the wall.
            var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var oc = outer.GetComponent<Collider>();
            if (oc != null) Object.Destroy(oc);
            outer.transform.SetParent(parent, false);
            outer.transform.position = new Vector3(0f, -0.06f, 0f);
            outer.transform.localScale = new Vector3(50f, 1f, 50f);
            outer.GetComponent<Renderer>().sharedMaterial = Visuals.Mat(new Color(0.08f, 0.13f, 0.1f));
        }

        static void Wall(Transform parent, Color c, Vector3 pos, Vector3 scale)
        {
            var w = Visuals.Prim(PrimitiveType.Cube, c, scale, parent);
            w.transform.position = pos;
        }
    }
}
