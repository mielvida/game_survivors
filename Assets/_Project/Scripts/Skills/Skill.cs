using System;
using System.Collections.Generic;

namespace Survivors
{
    public sealed class SkillDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool Active;
        public readonly string[] LevelText; // description of what each level adds
        public readonly Func<Skill> Create;

        public SkillDef(string id, string name, bool active, string[] levelText, Func<Skill> create)
        {
            Id = id;
            Name = name;
            Active = active;
            LevelText = levelText;
            Create = create;
        }

        public int MaxLevel { get { return LevelText.Length; } }
    }

    public abstract class Skill
    {
        public SkillDef Def;
        public int Level = 1;

        public abstract void Tick(float dt, Hero hero);
        public virtual void Dispose() { }
    }

    public sealed class PassiveSkill : Skill
    {
        public override void Tick(float dt, Hero hero) { }
    }

    public static class SkillLibrary
    {
        public static readonly SkillDef Bolt = new SkillDef("bolt", "마법 화살", true,
            new[] { "가까운 적에게 화살 1발", "피해 +20%", "화살 +1발", "관통 +1, 연사 속도 증가", "화살 +1발, 피해 증가" },
            () => new BoltSkill());

        public static readonly SkillDef Orbit = new SkillDef("orbit", "정령의 칼날", true,
            new[] { "주변을 도는 칼날 2개", "피해 +30%", "칼날 +1개", "피해 +25%", "칼날 +1개, 피해 증가" },
            () => new OrbitSkill());

        public static readonly SkillDef Pulse = new SkillDef("pulse", "신성한 파동", true,
            new[] { "주기적으로 주변에 피해", "범위 +15%", "주기 단축", "피해 +30%", "범위와 주기 대폭 강화" },
            () => new PulseSkill());

        public static readonly SkillDef Might = new SkillDef("might", "힘의 문장", false,
            new[] { "공격력 +15%", "공격력 +30%", "공격력 +45%", "공격력 +60%", "공격력 +75%" },
            () => new PassiveSkill());

        public static readonly SkillDef Haste = new SkillDef("haste", "신속의 반지", false,
            new[] { "공격 속도 +12%", "공격 속도 +24%", "공격 속도 +36%", "공격 속도 +48%", "공격 속도 +60%" },
            () => new PassiveSkill());

        public static readonly SkillDef Boots = new SkillDef("boots", "바람의 장화", false,
            new[] { "이동 속도 +0.5", "이동 속도 +1.0", "이동 속도 +1.5", "이동 속도 +2.0", "이동 속도 +2.5" },
            () => new PassiveSkill());

        public static readonly SkillDef Magnet = new SkillDef("magnet", "자석 구슬", false,
            new[] { "보석 흡수 범위 +1.2", "보석 흡수 범위 +2.4", "보석 흡수 범위 +3.6", "보석 흡수 범위 +4.8", "보석 흡수 범위 +6.0" },
            () => new PassiveSkill());

        public static readonly SkillDef Vital = new SkillDef("vital", "생명의 심장", false,
            new[] { "최대 체력 +20", "최대 체력 +40", "최대 체력 +60", "최대 체력 +80", "최대 체력 +100" },
            () => new PassiveSkill());

        // Offered when nothing else can be upgraded.
        public static readonly SkillDef Heal = new SkillDef("heal", "회복", false,
            new[] { "체력 30% 회복" }, () => new PassiveSkill());

        public static readonly SkillDef[] All = { Bolt, Orbit, Pulse, Might, Haste, Boots, Magnet, Vital };

        static Skill Find(IList<Skill> owned, SkillDef def)
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].Def == def) return owned[i];
            return null;
        }

        public static int LevelOf(IList<Skill> owned, SkillDef def)
        {
            var s = Find(owned, def);
            return s == null ? 0 : s.Level;
        }

        // Up to `count` distinct upgrade choices, respecting slot limits and max levels.
        public static List<SkillDef> Roll(IList<Skill> owned, int count, Random rng)
        {
            int actives = 0, passives = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Def.Active) actives++; else passives++;
            }

            var pool = new List<SkillDef>();
            foreach (var def in All)
            {
                var s = Find(owned, def);
                if (s != null) { if (s.Level < def.MaxLevel) pool.Add(def); }
                else if (def.Active ? actives < Config.MaxActiveSkills : passives < Config.MaxPassiveSkills) pool.Add(def);
            }

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }

            var result = new List<SkillDef>();
            for (int i = 0; i < pool.Count && result.Count < count; i++) result.Add(pool[i]);

            // Every level-up offers at least one attack skill when any is available.
            bool hasActive = false;
            for (int i = 0; i < result.Count; i++) if (result[i].Active) hasActive = true;
            if (!hasActive && result.Count > 0)
            {
                for (int i = result.Count; i < pool.Count; i++)
                {
                    if (!pool[i].Active) continue;
                    result[result.Count - 1] = pool[i];
                    break;
                }
            }
            if (result.Count == 0) result.Add(Heal);
            return result;
        }
    }
}
