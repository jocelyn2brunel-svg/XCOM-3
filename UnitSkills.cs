using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public enum SkillType { Endurance, Marksmanship, Strength, Tactics, Willpower }

    public sealed class SkillProgress
    {
        private const int BaseXPPerLevel = 100;
        private const float XPScaling = 1.5f;

        public int XP { get; private set; }
        public int Level => CalculateLevel(XP);
        public int XPToNext => XPForNextLevel(XP);

        public void AddXP(int amount)
        {
            if (amount > 0) XP += amount;
        }

        private static int CalculateLevel(int xp)
        {
            int level = 0, required = BaseXPPerLevel;
            while (xp >= required) { xp -= required; level++; required = (int)(required * XPScaling); }
            return level;
        }

        private static int XPForNextLevel(int xp)
        {
            int required = BaseXPPerLevel;
            while (xp >= required) { xp -= required; required = (int)(required * XPScaling); }
            return required - xp;
        }
    }

    public class UnitSkills
    {
        private readonly Dictionary<SkillType, SkillProgress> _skills =
            Enum.GetValues<SkillType>().ToDictionary(s => s, s => new SkillProgress());

        public SkillProgress this[SkillType skill] => _skills[skill];
        public int OverallLevel => _skills.Values.Sum(s => s.Level) / _skills.Count;

        public UnitSkills() { }

        public UnitSkills(UnitSkills other)
        {
            foreach (var kvp in other._skills)
                _skills[kvp.Key].AddXP(kvp.Value.XP);
        }

        // XP Gain
        public void GainMovementXP(int distance) => _skills[SkillType.Endurance].AddXP(distance * 5);

        public void GainShootingXP(bool hit, int distance, int damage)
        {
            _skills[SkillType.Marksmanship].AddXP(hit ? 15 + distance * 2 + damage / 2 : 3);
        }

        public void GainGrenadeXP(int enemiesHit, int totalDamage) =>
            _skills[SkillType.Strength].AddXP(10 + enemiesHit * 8 + totalDamage / 3);

        public void GainCoverXP() => _skills[SkillType.Tactics].AddXP(8);

        public void GainSurvivalXP(int damageTaken, bool survived) =>
            _skills[SkillType.Willpower].AddXP(damageTaken / 5 + (survived ? 15 : 0));

        public void GainKillXP(string enemyClass)
        {
            _skills[SkillType.Marksmanship].AddXP(20);
            _skills[SkillType.Willpower].AddXP(10);
        }

        // Bonuses
        public int GetMovementBonus() => _skills[SkillType.Endurance].Level / 3;
        public int GetAccuracyBonus() => _skills[SkillType.Marksmanship].Level * 2;
        public int GetDamageBonus() => _skills[SkillType.Strength].Level;
        public int GetDefenseBonus() => _skills[SkillType.Tactics].Level * 2;
        public int GetHealthBonus() => _skills[SkillType.Willpower].Level * 5;

        public string GetSkillsSummary() =>
            $"Niveau global: {OverallLevel}\n" +
            string.Join("\n", _skills.Select(kvp => $"{kvp.Key}: {kvp.Value.Level} ({kvp.Value.XP} XP)"));
    }
}
