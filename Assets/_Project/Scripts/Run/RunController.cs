using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    public enum RunPhase { Playing, LevelUp, Cleared, Failed }

    // Owns one stage run: timer, wave spawning, experience, level-up choices and skills.
    public sealed class RunController : MonoBehaviour
    {
        public static RunController I { get; private set; }

        public RunPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public int Level { get; private set; }
        public float Exp { get; private set; }
        public int Kills;
        public readonly List<Skill> Skills = new List<Skill>();
        public List<SkillDef> Choices { get; private set; }

        Hero _hero;
        int _pendingLevelUps;
        float _spawnTimer;
        bool _eliteSpawned;
        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            for (int i = 0; i < Skills.Count; i++) Skills[i].Dispose();
        }

        public void Init(Hero hero)
        {
            _hero = hero;
            Level = 1;
            Phase = RunPhase.Playing;
            GrantSkill(SkillLibrary.Bolt);
        }

        void GrantSkill(SkillDef def)
        {
            var s = def.Create();
            s.Def = def;
            Skills.Add(s);
            _hero.Stats.Recompute(Skills);
        }

        void Update()
        {
            if (Phase != RunPhase.Playing) return;
            float dt = Time.deltaTime;
            Elapsed += dt;

            Spawn(dt);
            for (int i = 0; i < Skills.Count; i++) Skills[i].Tick(dt, _hero);

            if (Elapsed >= Config.StageDuration)
            {
                Phase = RunPhase.Cleared;
                Time.timeScale = 0f;
            }
        }

        void Spawn(float dt)
        {
            var em = EnemyManager.I;
            float progress = Mathf.Clamp01(Elapsed / Config.StageDuration);

            if (!_eliteSpawned && Elapsed >= Config.EliteSpawnTime)
            {
                _eliteSpawned = true;
                SpawnOne(EnemyDefs.Brute);
            }

            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;
            _spawnTimer = Mathf.Lerp(0.8f, 0.15f, progress);

            int batch = 1 + (int)(Elapsed / 60f);
            for (int i = 0; i < batch && em.Active.Count < Config.MaxEnemies; i++)
            {
                var def = EnemyDefs.Slime;
                if (Elapsed > 45f && _rng.NextDouble() < 0.35) def = EnemyDefs.Skeleton;
                SpawnOne(def);
            }
        }

        void SpawnOne(EnemyDef def)
        {
            float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float x = _hero.X + Mathf.Cos(a) * Config.SpawnRadius;
            float z = _hero.Z + Mathf.Sin(a) * Config.SpawnRadius;
            float hpMul = 1f + Elapsed / 60f * 0.35f;
            EnemyManager.I.Spawn(def, x, z, hpMul);
        }

        public void AddExp(float value)
        {
            Exp += value;
            while (Exp >= ExpCurve.Required(Level))
            {
                Exp -= ExpCurve.Required(Level);
                Level++;
                _pendingLevelUps++;
            }
            if (_pendingLevelUps > 0 && Phase == RunPhase.Playing) OpenLevelUp();
        }

        void OpenLevelUp()
        {
            Choices = SkillLibrary.Roll(Skills, 3, _rng);
            Phase = RunPhase.LevelUp;
            Time.timeScale = 0f;
        }

        public void Pick(SkillDef def)
        {
            if (Phase != RunPhase.LevelUp) return;

            if (def == SkillLibrary.Heal)
            {
                _hero.Heal(_hero.Stats.MaxHp * 0.3f);
            }
            else
            {
                Skill owned = null;
                for (int i = 0; i < Skills.Count; i++)
                    if (Skills[i].Def == def) owned = Skills[i];

                float oldMax = _hero.Stats.MaxHp;
                if (owned == null) GrantSkill(def);
                else owned.Level = Mathf.Min(def.MaxLevel, owned.Level + 1);
                _hero.Stats.Recompute(Skills);
                _hero.OnMaxHpChanged(oldMax);
            }

            _pendingLevelUps--;
            if (_pendingLevelUps > 0)
            {
                Choices = SkillLibrary.Roll(Skills, 3, _rng);
            }
            else
            {
                Phase = RunPhase.Playing;
                Time.timeScale = 1f;
            }
        }

        public void Fail()
        {
            if (Phase == RunPhase.Failed || Phase == RunPhase.Cleared) return;
            Phase = RunPhase.Failed;
            Time.timeScale = 0f;
        }
    }
}
