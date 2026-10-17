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

    // Static square arena shaped like a floating island: checkerboard floor and a cliff edge.
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

            // Cliff face: stacked rock layers dropping below the edge, plus a void floor far below.
            var strata = new[] { new Color(0.36f, 0.31f, 0.27f), new Color(0.42f, 0.36f, 0.30f), new Color(0.31f, 0.27f, 0.25f), new Color(0.45f, 0.40f, 0.35f) };
            float len = size + 2f;
            for (int layer = 0; layer < 7; layer++)
            {
                float y = -layer * 2f - 1f;
                float outer = 0.4f + layer * 0.55f;
                float thick = 1.6f + layer * 0.3f;
                var c = strata[layer % strata.Length];
                Wall(parent, c, new Vector3(0f, y, -half - outer), new Vector3(len, 2.2f, thick));
                Wall(parent, c, new Vector3(0f, y, half + outer), new Vector3(len, 2.2f, thick));
                Wall(parent, c, new Vector3(-half - outer, y, 0f), new Vector3(thick, 2.2f, len));
                Wall(parent, c, new Vector3(half + outer, y, 0f), new Vector3(thick, 2.2f, len));
            }

            // Rock lip: boulders along the edge, some with a mossy cap.
            var rng = new System.Random(7);
            var rock = new[] { new Color(0.43f, 0.42f, 0.39f), new Color(0.49f, 0.47f, 0.43f), new Color(0.37f, 0.36f, 0.34f) };
            var moss = new Color(0.25f, 0.49f, 0.27f);
            for (int side = 0; side < 4; side++)
            {
                for (float k = -half; k <= half; k += 1.7f)
                {
                    float jitter = (float)rng.NextDouble() * 0.9f;
                    float s1 = 0.9f + (float)rng.NextDouble() * 1.1f;
                    float hh = 0.5f + (float)rng.NextDouble() * 1.0f;
                    float off = half + 0.3f + jitter;
                    float along = k + (float)rng.NextDouble();
                    Vector3 pos = side == 0 ? new Vector3(along, hh * 0.4f, -off)
                                : side == 1 ? new Vector3(along, hh * 0.4f, off)
                                : side == 2 ? new Vector3(-off, hh * 0.4f, along)
                                : new Vector3(off, hh * 0.4f, along);
                    var boulder = Visuals.Prim(PrimitiveType.Sphere, rock[rng.Next(rock.Length)], new Vector3(s1 * 2f, hh * 2f, s1 * 2f), parent);
                    boulder.transform.position = pos;
                    if (rng.NextDouble() < 0.55)
                    {
                        var cap = Visuals.Prim(PrimitiveType.Sphere, moss, new Vector3(s1 * 1.6f, hh * 0.6f, s1 * 1.4f), parent);
                        cap.transform.position = pos + new Vector3(0f, hh * 0.75f, 0f);
                    }
                }
            }

            // Corner crystals.
            var crystal = new Color(0.48f, 0.55f, 1f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var cr = Visuals.Prim(PrimitiveType.Cube, crystal, new Vector3(0.9f, 3.2f, 0.9f), parent);
                    cr.transform.position = new Vector3(sx * (half + 0.7f), 1.6f, sz * (half + 0.7f));
                    cr.transform.rotation = Quaternion.Euler(0f, 45f, 8f);
                }

            // Void floor far below the island.
            var voidPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var vc = voidPlane.GetComponent<Collider>();
            if (vc != null) Object.Destroy(vc);
            voidPlane.transform.SetParent(parent, false);
            voidPlane.transform.position = new Vector3(0f, -16f, 0f);
            voidPlane.transform.localScale = new Vector3(40f, 1f, 40f);
            voidPlane.GetComponent<Renderer>().sharedMaterial = Visuals.Mat(new Color(0.1f, 0.14f, 0.2f));
        }

        static void Wall(Transform parent, Color c, Vector3 pos, Vector3 scale)
        {
            var w = Visuals.Prim(PrimitiveType.Cube, c, scale, parent);
            w.transform.position = pos;
        }
    }
}
