using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class GemManager : MonoBehaviour
    {
        public static GemManager I { get; private set; }

        sealed class Gem
        {
            public GameObject Go;
            public float X, Z, Value;
            public bool Attracted;
        }

        readonly List<Gem> _gems = new List<Gem>();
        Pool _pool;

        void Awake()
        {
            I = this;
            _pool = new Pool(() => Visuals.Prim(PrimitiveType.Sphere, new Color(0.35f, 0.85f, 1f), new Vector3(0.3f, 0.3f, 0.3f), transform));
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Drop(float x, float z, float value)
        {
            var go = _pool.Get();
            go.transform.position = new Vector3(x, 0.3f, z);
            _gems.Add(new Gem { Go = go, X = x, Z = z, Value = value });
        }

        void Update()
        {
            var hero = Hero.I;
            var run = RunController.I;
            if (hero == null || run == null) return;
            float dt = Time.deltaTime;
            float magnet = hero.Stats.Magnet;

            for (int i = _gems.Count - 1; i >= 0; i--)
            {
                var g = _gems[i];
                float dx = hero.X - g.X, dz = hero.Z - g.Z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < magnet) g.Attracted = true;

                if (g.Attracted && dist > 0.001f)
                {
                    float sp = 10f + 8f * Mathf.Clamp01(1f - dist / magnet);
                    g.X += dx / dist * sp * dt;
                    g.Z += dz / dist * sp * dt;
                    g.Go.transform.position = new Vector3(g.X, 0.3f, g.Z);
                }

                if (dist < 0.6f)
                {
                    _pool.Release(g.Go);
                    int last = _gems.Count - 1;
                    _gems[i] = _gems[last];
                    _gems.RemoveAt(last);
                    run.AddExp(g.Value);
                }
            }
        }
    }
}
