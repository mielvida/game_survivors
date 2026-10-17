using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Survivors.Tests
{
    public class ExpCurveTests
    {
        [Test]
        public void RequiredIsPositiveAndIncreasing()
        {
            int prev = 0;
            for (int level = 1; level <= 50; level++)
            {
                int need = ExpCurve.Required(level);
                Assert.Greater(need, prev);
                prev = need;
            }
        }
    }

    public class SpatialHashTests
    {
        [Test]
        public void QueryFindsNearbyAndSkipsFar()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(0, 0f, 0f);
            hash.Insert(1, 1.5f, 0.5f);
            hash.Insert(2, 50f, 50f);
            hash.Insert(3, -3f, -3f);

            var result = new List<int>();
            hash.Query(0f, 0f, 2f, result);

            CollectionAssert.Contains(result, 0);
            CollectionAssert.Contains(result, 1);
            CollectionAssert.DoesNotContain(result, 2);
        }

        [Test]
        public void ClearRemovesEntries()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(0, 0f, 0f);
            hash.Clear();

            var result = new List<int>();
            hash.Query(0f, 0f, 5f, result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void NegativeCoordinatesAreHandled()
        {
            var hash = new SpatialHash(2f);
            hash.Insert(7, -0.5f, -0.5f);

            var result = new List<int>();
            hash.Query(0.5f, 0.5f, 1.5f, result);
            CollectionAssert.Contains(result, 7);
        }
    }

    public class SkillLibraryTests
    {
        [Test]
        public void RollReturnsDistinctChoices()
        {
            var owned = new List<Skill>();
            var picks = SkillLibrary.Roll(owned, 3, new Random(1));
            Assert.AreEqual(3, picks.Count);
            CollectionAssert.AllItemsAreUnique(picks);
        }

        [Test]
        public void RollAlwaysOffersAnAttackSkill()
        {
            var owned = new List<Skill>
            {
                new PassiveSkill { Def = SkillLibrary.Might },
                new PassiveSkill { Def = SkillLibrary.Haste },
                new PassiveSkill { Def = SkillLibrary.Boots },
            };
            for (int seed = 0; seed < 100; seed++)
            {
                var picks = SkillLibrary.Roll(owned, 3, new Random(seed));
                Assert.IsTrue(picks.Exists(d => d.Active), "seed " + seed);
            }
        }

        [Test]
        public void RollFallsBackToHealWhenEverythingIsMaxed()
        {
            var owned = new List<Skill>();
            foreach (var def in SkillLibrary.All)
                owned.Add(new PassiveSkill { Def = def, Level = def.MaxLevel });

            var picks = SkillLibrary.Roll(owned, 3, new Random(2));
            Assert.AreEqual(1, picks.Count);
            Assert.AreSame(SkillLibrary.Heal, picks[0]);
        }
    }
}
