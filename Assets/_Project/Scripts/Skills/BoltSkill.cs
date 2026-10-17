using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class BoltSkill : Skill
    {
        static readonly int[] Count = { 1, 1, 2, 2, 3 };
        static readonly float[] Cooldown = { 0.9f, 0.85f, 0.85f, 0.7f, 0.65f };
        static readonly float[] Damage = { 14f, 16f, 16f, 19f, 22f };
        static readonly int[] Pierce = { 1, 1, 1, 2, 3 };

        const float Range = 12f;
        const float Speed = 14f;

        sealed class Bolt
        {
            public GameObject Go;
            public float X, Z, Dx, Dz, Life, Dmg;
            public int Pierce;
            public readonly List<Enemy> Hit = new List<Enemy>(4);
        }

        readonly List<Bolt> _bolts = new List<Bolt>();
        readonly List<Enemy> _near = new List<Enemy>();
        readonly List<Enemy> _tmp = new List<Enemy>();
        readonly Pool _pool;
        float _timer;
        float _px, _pz;

        public BoltSkill()
        {
            _pool = new Pool(() => Visuals.Prim(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.3f), new Vector3(0.35f, 0.35f, 0.35f), Bootstrap.Root.transform));
        }

        public override void Tick(float dt, Hero hero)
        {
            _timer -= dt * hero.Stats.AtkSpeedMul;
            if (_timer <= 0f && Fire(hero)) _timer = Cooldown[Level - 1];
            UpdateBolts(dt, hero);
        }

        int CompareByDistance(Enemy a, Enemy b)
        {
            float da = (a.X - _px) * (a.X - _px) + (a.Z - _pz) * (a.Z - _pz);
            float db = (b.X - _px) * (b.X - _px) + (b.Z - _pz) * (b.Z - _pz);
            return da.CompareTo(db);
        }

        bool Fire(Hero hero)
        {
            EnemyManager.I.QueryCircle(hero.X, hero.Z, Range, _near);
            if (_near.Count == 0) return false;
            _px = hero.X;
            _pz = hero.Z;
            _near.Sort(CompareByDistance);

            int n = Count[Level - 1];
            for (int i = 0; i < n; i++)
            {
                var target = _near[Mathf.Min(i, _near.Count - 1)];
                float dx = target.X - hero.X, dz = target.Z - hero.Z;
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 0.001f) { dx = 0f; dz = 1f; len = 1f; }
                dx /= len; dz /= len;

                if (i >= _near.Count) // more bolts than targets: fan them out
                {
                    float ang = (i % 2 == 0 ? 1f : -1f) * 10f * Mathf.Deg2Rad * (i - _near.Count + 1);
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    float rx = dx * cs - dz * sn, rz = dx * sn + dz * cs;
                    dx = rx; dz = rz;
                }

                var b = new Bolt
                {
                    Go = _pool.Get(), X = hero.X, Z = hero.Z, Dx = dx, Dz = dz,
                    Life = Range / Speed + 0.3f,
                    Dmg = Damage[Level - 1] * hero.Stats.DamageMul,
                    Pierce = Pierce[Level - 1]
                };
                b.Go.transform.position = new Vector3(b.X, 0.8f, b.Z);
                _bolts.Add(b);
            }
            return true;
        }

        void UpdateBolts(float dt, Hero hero)
        {
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                b.X += b.Dx * Speed * dt;
                b.Z += b.Dz * Speed * dt;
                b.Life -= dt;
                b.Go.transform.position = new Vector3(b.X, 0.8f, b.Z);

                EnemyManager.I.QueryCircle(b.X, b.Z, 0.25f, _tmp);
                for (int k = 0; k < _tmp.Count && b.Pierce > 0; k++)
                {
                    var e = _tmp[k];
                    if (b.Hit.Contains(e)) continue;
                    b.Hit.Add(e);
                    EnemyManager.I.Damage(e, b.Dmg);
                    b.Pierce--;
                }

                if (b.Pierce <= 0 || b.Life <= 0f)
                {
                    _pool.Release(b.Go);
                    _bolts.RemoveAt(i);
                }
            }
        }
    }
}
