using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class PulseSkill : Skill
    {
        static readonly float[] Interval = { 3.0f, 3.0f, 2.5f, 2.5f, 1.8f };
        static readonly float[] Radius = { 3.0f, 3.5f, 3.5f, 3.5f, 5.0f };
        static readonly float[] Damage = { 12f, 15f, 18f, 24f, 28f };

        const float FlashTime = 0.18f;

        readonly List<Enemy> _tmp = new List<Enemy>();
        GameObject _flash;
        float _timer = 1f;
        float _flashLeft;

        public override void Tick(float dt, Hero hero)
        {
            if (_flash == null)
            {
                _flash = Visuals.Prim(PrimitiveType.Cylinder, new Color(1f, 0.95f, 0.6f), Vector3.one, Bootstrap.Root.transform);
                _flash.SetActive(false);
            }

            _timer -= dt * hero.Stats.AtkSpeedMul;
            if (_timer <= 0f)
            {
                _timer = Interval[Level - 1];
                float r = Radius[Level - 1];
                EnemyManager.I.QueryCircle(hero.X, hero.Z, r, _tmp);
                for (int i = 0; i < _tmp.Count; i++)
                    EnemyManager.I.Damage(_tmp[i], Damage[Level - 1] * hero.Stats.DamageMul);

                _flash.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
                _flash.SetActive(true);
                _flashLeft = FlashTime;
            }

            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                _flash.transform.position = new Vector3(hero.X, 0.05f, hero.Z);
                if (_flashLeft <= 0f) _flash.SetActive(false);
            }
        }
    }
}
