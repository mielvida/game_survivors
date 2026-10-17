using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class OrbitSkill : Skill
    {
        static readonly int[] Count = { 2, 2, 3, 3, 4 };
        static readonly float[] Damage = { 6f, 8f, 8f, 10f, 12f };

        const float OrbitRadius = 3.2f;
        const float DegPerSec = 200f;
        const float HitInterval = 0.4f;

        readonly List<GameObject> _blades = new List<GameObject>();
        readonly Dictionary<Enemy, float> _nextHit = new Dictionary<Enemy, float>();
        readonly List<Enemy> _tmp = new List<Enemy>();
        readonly List<Enemy> _stale = new List<Enemy>();
        float _angle;
        float _purgeTimer;

        public override void Tick(float dt, Hero hero)
        {
            int n = Count[Level - 1];
            while (_blades.Count < n)
            {
                var g = Visuals.Prim(PrimitiveType.Cube, new Color(0.6f, 0.9f, 1f), new Vector3(0.3f, 0.25f, 1.5f), Bootstrap.Root.transform);
                _blades.Add(g);
            }

            _angle += DegPerSec * dt;
            float now = Time.time;
            for (int i = 0; i < _blades.Count; i++)
            {
                float a = (_angle + 360f * i / _blades.Count) * Mathf.Deg2Rad;
                float x = hero.X + Mathf.Cos(a) * OrbitRadius;
                float z = hero.Z + Mathf.Sin(a) * OrbitRadius;
                _blades[i].transform.position = new Vector3(x, 0.8f, z);
                _blades[i].transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);

                EnemyManager.I.QueryCircle(x, z, 0.9f, _tmp);
                for (int k = 0; k < _tmp.Count; k++)
                {
                    var e = _tmp[k];
                    float next;
                    if (_nextHit.TryGetValue(e, out next) && now < next) continue;
                    _nextHit[e] = now + HitInterval;
                    EnemyManager.I.Damage(e, Damage[Level - 1] * hero.Stats.DamageMul);
                }
            }

            _purgeTimer -= dt;
            if (_purgeTimer <= 0f)
            {
                _purgeTimer = 2f;
                _stale.Clear();
                foreach (var kv in _nextHit)
                    if (kv.Key.Dead) _stale.Add(kv.Key);
                for (int i = 0; i < _stale.Count; i++) _nextHit.Remove(_stale[i]);
            }
        }
    }
}
