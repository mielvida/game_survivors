using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public sealed class RunStats
    {
        public float DamageMul = 1f;
        public float AtkSpeedMul = 1f;
        public float MoveSpeed = 5f;
        public float Magnet = 2.5f;
        public float MaxHp = 100f;

        // Derived from the passive skills the hero owns.
        public void Recompute(IList<Skill> skills)
        {
            DamageMul = 1f;
            AtkSpeedMul = 1f;
            MoveSpeed = 5f;
            Magnet = 2.5f;
            MaxHp = 100f;
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                switch (s.Def.Id)
                {
                    case "might": DamageMul += 0.15f * s.Level; break;
                    case "haste": AtkSpeedMul += 0.12f * s.Level; break;
                    case "boots": MoveSpeed += 0.5f * s.Level; break;
                    case "magnet": Magnet += 1.2f * s.Level; break;
                    case "vital": MaxHp += 20f * s.Level; break;
                }
            }
        }
    }

    [DefaultExecutionOrder(-200)]
    public sealed class Hero : MonoBehaviour
    {
        public static Hero I { get; private set; }

        public const float Radius = 0.5f;

        public readonly RunStats Stats = new RunStats();
        public float X, Z, Hp;

        Transform _visual;
        Vector3 _facing = Vector3.forward;

        public bool Dead { get { return Hp <= 0f; } }

        void Awake()
        {
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Build()
        {
            var v = new GameObject("Visual").transform;
            v.SetParent(transform, false);
            _visual = v;
            Visuals.Prim(PrimitiveType.Capsule, new Color(0.30f, 0.45f, 0.85f), new Vector3(0.9f, 0.6f, 0.9f), v).transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Visuals.Prim(PrimitiveType.Sphere, new Color(0.88f, 0.88f, 0.92f), new Vector3(0.55f, 0.55f, 0.55f), v).transform.localPosition = new Vector3(0f, 1.45f, 0f);
            Visuals.Prim(PrimitiveType.Cube, new Color(0.85f, 0.2f, 0.2f), new Vector3(0.12f, 0.3f, 0.3f), v).transform.localPosition = new Vector3(0f, 1.85f, -0.05f);
            Visuals.Prim(PrimitiveType.Cube, new Color(0.95f, 0.95f, 1f), new Vector3(0.1f, 0.1f, 1f), v).transform.localPosition = new Vector3(0.55f, 0.9f, 0.5f);
            Hp = Stats.MaxHp;
        }

        void Update()
        {
            InputService.Tick();
            if (Dead) return;

            float dt = Time.deltaTime;
            Vector2 m = InputService.Move;
            X += m.x * Stats.MoveSpeed * dt;
            Z += m.y * Stats.MoveSpeed * dt;
            transform.position = new Vector3(X, 0f, Z);

            if (m.sqrMagnitude > 0.01f)
            {
                _facing = new Vector3(m.x, 0f, m.y);
                _visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.08f, 0f);
            }
            _visual.rotation = Quaternion.Slerp(_visual.rotation, Quaternion.LookRotation(_facing), 1f - Mathf.Exp(-14f * dt));
        }

        public void TakeDamage(float amount)
        {
            if (Dead) return;
            Hp -= amount;
            if (Hp <= 0f)
            {
                Hp = 0f;
                if (RunController.I != null) RunController.I.Fail();
            }
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(Stats.MaxHp, Hp + amount);
        }

        public void OnMaxHpChanged(float oldMax)
        {
            float delta = Stats.MaxHp - oldMax;
            if (delta > 0f) Hp += delta;
            Hp = Mathf.Min(Hp, Stats.MaxHp);
        }
    }
}
