using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class EnemyDef
    {
        public string Name;
        public float Hp, Speed, Damage, Radius, Exp;
        public Color Color;
        public Vector3 Scale;
        public PrimitiveType Shape;

        public float BaseY
        {
            get { return Shape == PrimitiveType.Capsule ? Scale.y : Scale.y * 0.5f; }
        }
    }

    public static class EnemyDefs
    {
        public static readonly EnemyDef Slime = new EnemyDef
        {
            Name = "slime", Hp = 12f, Speed = 2.0f, Damage = 5f, Radius = 0.4f, Exp = 1f,
            Color = new Color(0.35f, 0.75f, 0.35f), Scale = new Vector3(0.9f, 0.7f, 0.9f), Shape = PrimitiveType.Sphere
        };
        public static readonly EnemyDef Skeleton = new EnemyDef
        {
            Name = "skeleton", Hp = 32f, Speed = 2.5f, Damage = 8f, Radius = 0.4f, Exp = 2f,
            Color = new Color(0.88f, 0.86f, 0.75f), Scale = new Vector3(0.6f, 0.6f, 0.6f), Shape = PrimitiveType.Capsule
        };
        public static readonly EnemyDef Brute = new EnemyDef
        {
            Name = "brute", Hp = 550f, Speed = 2.0f, Damage = 16f, Radius = 0.8f, Exp = 25f,
            Color = new Color(0.7f, 0.22f, 0.2f), Scale = new Vector3(1.5f, 1.5f, 1.5f), Shape = PrimitiveType.Cube
        };
    }

    public sealed class Enemy
    {
        public EnemyDef Def;
        public GameObject Go;
        public Transform Tr;
        public float X, Z, Hp, AttackCd;
        public bool Dead;
    }

    // Central update for all enemies (no per-enemy MonoBehaviour). Runs before
    // other gameplay scripts so the spatial hash is fresh when skills query it.
    [DefaultExecutionOrder(-100)]
    public sealed class EnemyManager : MonoBehaviour
    {
        public static EnemyManager I { get; private set; }

        public readonly List<Enemy> Active = new List<Enemy>();

        readonly SpatialHash _hash = new SpatialHash(2f);
        readonly List<int> _ids = new List<int>();
        readonly Dictionary<EnemyDef, Pool> _pools = new Dictionary<EnemyDef, Pool>();

        void Awake()
        {
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public Enemy Spawn(EnemyDef def, float x, float z, float hpMul)
        {
            Pool pool;
            if (!_pools.TryGetValue(def, out pool))
            {
                var d = def;
                pool = new Pool(() => Visuals.Prim(d.Shape, d.Color, d.Scale, transform));
                _pools[def] = pool;
            }
            var go = pool.Get();
            var e = new Enemy { Def = def, Go = go, Tr = go.transform, X = x, Z = z, Hp = def.Hp * hpMul };
            e.Tr.position = new Vector3(x, def.BaseY, z);
            Active.Add(e);
            return e;
        }

        void Update()
        {
            var hero = Hero.I;
            if (hero == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;

            _hash.Clear();
            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (!e.Dead) _hash.Insert(i, e.X, e.Z);
            }

            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (e.Dead) continue;

                float dx = hero.X - e.X, dz = hero.Z - e.Z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float nx = dist > 0.001f ? dx / dist : 0f;
                float nz = dist > 0.001f ? dz / dist : 0f;

                // Separation from close neighbours so the crowd spreads out.
                float px = 0f, pz = 0f;
                float minDist = e.Def.Radius * 2f;
                _ids.Clear();
                _hash.Query(e.X, e.Z, minDist, _ids);
                for (int k = 0; k < _ids.Count; k++)
                {
                    int j = _ids[k];
                    if (j == i) continue;
                    var o = Active[j];
                    float ox = e.X - o.X, oz = e.Z - o.Z;
                    float d2 = ox * ox + oz * oz;
                    if (d2 < minDist * minDist && d2 > 0.0001f)
                    {
                        float d = Mathf.Sqrt(d2);
                        float push = (minDist - d) / minDist;
                        px += ox / d * push;
                        pz += oz / d * push;
                    }
                }

                float sp = e.Def.Speed;
                e.X += (nx * sp + px * sp * 1.5f) * dt;
                e.Z += (nz * sp + pz * sp * 1.5f) * dt;

                e.Tr.position = new Vector3(e.X, e.Def.BaseY + Mathf.Abs(Mathf.Sin(t * 7f + i)) * 0.12f, e.Z);
                if (dist > 0.001f) e.Tr.rotation = Quaternion.LookRotation(new Vector3(nx, 0f, nz));

                e.AttackCd -= dt;
                if (e.AttackCd <= 0f && dist < e.Def.Radius + Hero.Radius)
                {
                    hero.TakeDamage(e.Def.Damage);
                    e.AttackCd = 0.9f;
                }
            }
        }

        void LateUpdate()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var e = Active[i];
                if (!e.Dead) continue;
                _pools[e.Def].Release(e.Go);
                int last = Active.Count - 1;
                Active[i] = Active[last];
                Active.RemoveAt(last);
            }
        }

        // Enemies whose body overlaps the circle. Valid for the current frame.
        public void QueryCircle(float x, float z, float radius, List<Enemy> results)
        {
            results.Clear();
            _ids.Clear();
            _hash.Query(x, z, radius + 1f, _ids);
            for (int k = 0; k < _ids.Count; k++)
            {
                var e = Active[_ids[k]];
                if (e.Dead) continue;
                float dx = e.X - x, dz = e.Z - z;
                float r = radius + e.Def.Radius;
                if (dx * dx + dz * dz <= r * r) results.Add(e);
            }
        }

        public int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < Active.Count; i++)
                if (!Active[i].Dead) n++;
            return n;
        }

        public void Damage(Enemy e, float amount)
        {
            if (e.Dead) return;
            e.Hp -= amount;
            if (e.Hp > 0f) return;
            e.Dead = true;
            if (GemManager.I != null) GemManager.I.Drop(e.X, e.Z, e.Def.Exp);
            if (RunController.I != null) RunController.I.OnEnemyKilled();
        }
    }
}
