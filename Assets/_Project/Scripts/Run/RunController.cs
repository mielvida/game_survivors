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
        public int Wave { get; private set; }               // 0-based
        public int WaveCount { get { return _waves.Length; } }
        public string BannerText { get; private set; }
        public float BannerTime { get; private set; }
        public readonly List<Skill> Skills = new List<Skill>();
        public List<SkillDef> Choices { get; private set; }

        Hero _hero;
        int _pendingLevelUps;
        readonly WaveDef[] _waves = StageDefs.Stage1;
        int _spawned, _waveKills;
        float _spawnTimer, _waveTime, _intermission;
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
            StartWave(0);
        }

        public float WaveProgress
        {
            get { return Mathf.Clamp01((float)_waveKills / _waves[Wave].Quota); }
        }

        public void OnEnemyKilled()
        {
            Kills++;
            _waveKills++;
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

            WaveTick(dt);
            for (int i = 0; i < Skills.Count; i++) Skills[i].Tick(dt, _hero);
        }

        void StartWave(int index)
        {
            Wave = index;
            _spawned = 0;
            _waveKills = 0;
            _waveTime = 0f;
            _spawnTimer = 0.5f;
            var w = _waves[index];
            BannerText = "WAVE " + (index + 1) + (w.Boss ? "  ·  엘리트 등장" : "");
            BannerTime = 2f;
            if (w.Boss)
            {
                SpawnOne(EnemyDefs.Brute);
                _spawned = 1;
            }
        }

        void WaveTick(float dt)
        {
            if (BannerTime > 0f) BannerTime -= dt;
            if (_intermission > 0f)
            {
                _intermission -= dt;
                if (_intermission <= 0f) StartWave(Wave + 1);
                return;
            }

            var em = EnemyManager.I;
            var w = _waves[Wave];
            bool last = Wave == _waves.Length - 1;
            _waveTime += dt;

            if (_spawned < w.Quota)
            {
                _spawnTimer -= dt;
                if (_spawnTimer <= 0f)
                {
                    _spawnTimer = w.Interval;
                    for (int i = 0; i < w.Batch && _spawned < w.Quota && em.Active.Count < Config.MaxEnemies; i++)
                    {
                        bool skeleton = _rng.NextDouble() < w.SkeletonChance;
                        SpawnOne(skeleton ? EnemyDefs.Skeleton : EnemyDefs.Slime);
                        _spawned++;
                    }
                }
            }

            bool allSpawned = _spawned >= w.Quota;
            bool cleared = allSpawned && em.AliveCount() == 0;
            bool timeout = allSpawned && !last && _waveTime > w.TimeLimit;
            if (!cleared && !timeout) return;

            if (last)
            {
                Phase = RunPhase.Cleared;
                Time.timeScale = 0f;
            }
            else
            {
                _intermission = StageDefs.IntermissionSeconds;
                _hero.Heal(_hero.Stats.MaxHp * 0.1f);
                BannerText = "웨이브 " + (Wave + 1) + " 클리어!";
                BannerTime = 2.5f;
            }
        }

        void SpawnOne(EnemyDef def)
        {
            // Pick a point on the spawn ring that lies inside the arena; clamp as a fallback.
            float lim = Config.ArenaHalf - 1f;
            float x = 0f, z = 0f;
            for (int tries = 0; tries < 14; tries++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                x = _hero.X + Mathf.Cos(a) * Config.SpawnRadius;
                z = _hero.Z + Mathf.Sin(a) * Config.SpawnRadius;
                if (Mathf.Abs(x) < lim && Mathf.Abs(z) < lim) break;
            }
            x = Mathf.Clamp(x, -lim, lim);
            z = Mathf.Clamp(z, -lim, lim);
            float hpMul = 1f + 0.15f * Wave;
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
