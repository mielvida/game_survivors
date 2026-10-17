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

    // Static square arena: checkerboard floor closed in by a natural thicket of bushes, boulders and trees.
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

            // Natural boundary: bushes and boulders hug the edge, trees stand behind them.
            var rng = new System.Random(7);
            var rock = new[] { new Color(0.47f, 0.45f, 0.44f), new Color(0.52f, 0.51f, 0.48f), new Color(0.42f, 0.41f, 0.38f) };
            var leaf = new[] { new Color(0.18f, 0.42f, 0.24f), new Color(0.23f, 0.49f, 0.27f), new Color(0.16f, 0.36f, 0.22f) };
            var pine = new[] { new Color(0.12f, 0.30f, 0.22f), new Color(0.16f, 0.38f, 0.25f), new Color(0.18f, 0.45f, 0.29f) };
            var trunk = new Color(0.42f, 0.29f, 0.17f);

            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float sign = (side % 2 == 0) ? -1f : 1f;
                bool near = side == 1; // bottom of the screen: keep things low so the hero stays visible

                for (float k = -half - 3f; k <= half + 3f; k += 1.15f)
                {
                    float sc = 0.8f + (float)rng.NextDouble() * 0.6f;
                    float off = half + 1f + (float)rng.NextDouble() * 0.8f;
                    float along = k + (float)rng.NextDouble() * 0.6f;
                    var scale = new Vector3(sc * 2.2f, sc * (near ? 1.4f : 2.0f), sc * 2.2f);
                    bool isRock = rng.NextDouble() < 0.42;
                    var blob = Visuals.Prim(PrimitiveType.Sphere, isRock ? rock[rng.Next(rock.Length)] : leaf[rng.Next(leaf.Length)], scale, parent);
                    blob.transform.position = EdgePoint(alongX, sign, along, off, scale.y * 0.4f);
                }

                for (float k = -half - 4f; k <= half + 4f; k += 2.1f)
                    Tree(parent, rng, pine, trunk, EdgePoint(alongX, sign, k + (float)rng.NextDouble() * 1.2f, half + (near ? 4f : 3f) + (float)rng.NextDouble() * 2.2f, 0f));

                for (float k = -half - 10f; k <= half + 10f; k += 4f)
                    Tree(parent, rng, pine, trunk, EdgePoint(alongX, sign, k + (float)rng.NextDouble() * 2f, half + 6f + (float)rng.NextDouble() * 24f, 0f));
            }

            // Dark forest floor beyond the thicket.
            var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var oc = outer.GetComponent<Collider>();
            if (oc != null) Object.Destroy(oc);
            outer.transform.SetParent(parent, false);
            outer.transform.position = new Vector3(0f, -0.06f, 0f);
            outer.transform.localScale = new Vector3(50f, 1f, 50f);
            outer.GetComponent<Renderer>().sharedMaterial = Visuals.Mat(new Color(0.08f, 0.13f, 0.1f));
        }

        static Vector3 EdgePoint(bool alongX, float sign, float along, float off, float y)
        {
            return alongX ? new Vector3(along, y, sign * off) : new Vector3(sign * off, y, along);
        }

        static void Tree(Transform parent, System.Random rng, Color[] canopy, Color trunk, Vector3 pos)
        {
            float t = 1f + (float)rng.NextDouble() * 0.9f;
            var stem = Visuals.Prim(PrimitiveType.Cylinder, trunk, new Vector3(0.45f * t, 0.7f * t, 0.45f * t), parent);
            stem.transform.position = pos + new Vector3(0f, 0.7f * t, 0f);
            var low = Visuals.Prim(PrimitiveType.Sphere, canopy[rng.Next(canopy.Length)], new Vector3(2.4f * t, 2.2f * t, 2.4f * t), parent);
            low.transform.position = pos + new Vector3(0f, 1.9f * t, 0f);
            var top = Visuals.Prim(PrimitiveType.Sphere, canopy[rng.Next(canopy.Length)], new Vector3(1.6f * t, 2.0f * t, 1.6f * t), parent);
            top.transform.position = pos + new Vector3(0f, 3.5f * t, 0f);
        }
    }
}
