using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public enum SkillType
    {
        Endurance,
        Marksmanship,
        Strength,
        Tactics,
        Willpower
    }

    /// <summary>
    /// Gère l'XP et la progression d'une compétence unique
    /// </summary>
    public sealed class SkillProgress
    {
        public int XP { get; private set; }

        public int Level => CalculateLevel(XP);
        public int XPToNext => XPForNextLevel(XP);

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            XP += amount;
        }

        #region Progression math
        private const int BaseXPPerLevel = 100;
        private const float XPScaling = 1.5f;

        private static int CalculateLevel(int xp)
        {
            int level = 0;
            int required = BaseXPPerLevel;

            while (xp >= required)
            {
                xp -= required;
                level++;
                required = (int)(required * XPScaling);
            }

            return level;
        }

        private static int XPForNextLevel(int xp)
        {
            int required = BaseXPPerLevel;

            while (xp >= required)
            {
                xp -= required;
                required = (int)(required * XPScaling);
            }

            return required - xp;
        }
        #endregion
    }

    /// <summary>
    /// Système global de compétences pour une unité
    /// </summary>
    public class UnitSkills
    {
        private readonly Dictionary<SkillType, SkillProgress> _skills;

        public SkillProgress this[SkillType skill] => _skills[skill];

        public int OverallLevel =>
            (GetLevel(SkillType.Endurance)
           + GetLevel(SkillType.Marksmanship)
           + GetLevel(SkillType.Strength)
           + GetLevel(SkillType.Tactics)
           + GetLevel(SkillType.Willpower)) / 5;

        public UnitSkills()
        {
            _skills = new Dictionary<SkillType, SkillProgress>();
            foreach (SkillType skill in Enum.GetValues(typeof(SkillType)))
                _skills[skill] = new SkillProgress();
        }

        public UnitSkills(UnitSkills other) : this()
        {
            foreach (var kvp in other._skills)
                _skills[kvp.Key].AddXP(kvp.Value.XP);
        }

        #region XP Gain
        public void GainMovementXP(int distance)
        {
            AddXP(SkillType.Endurance, distance * 5);
        }

        public void GainShootingXP(bool hit, int distance, int damage)
        {
            int xp = hit
                ? 15 + distance * 2 + damage / 2
                : 3;

            AddXP(SkillType.Marksmanship, xp);
        }

        public void GainGrenadeXP(int enemiesHit, int totalDamage)
        {
            AddXP(SkillType.Strength, 10 + enemiesHit * 8 + totalDamage / 3);
        }

        public void GainCoverXP()
        {
            AddXP(SkillType.Tactics, 8);
        }

        public void GainSurvivalXP(int damageTaken, bool survived)
        {
            AddXP(SkillType.Willpower, damageTaken / 5 + (survived ? 15 : 0));
        }

        public void GainKillXP(string enemyClass)
        {
            AddXP(SkillType.Marksmanship, 20);
            AddXP(SkillType.Willpower, 10);
        }
        #endregion

        #region Bonuses
        public int GetMovementBonus() => GetLevel(SkillType.Endurance) / 3;
        public int GetAccuracyBonus() => GetLevel(SkillType.Marksmanship) * 2;
        public int GetDamageBonus() => GetLevel(SkillType.Strength);
        public int GetDefenseBonus() => GetLevel(SkillType.Tactics) * 2;
        public int GetHealthBonus() => GetLevel(SkillType.Willpower) * 5;
        #endregion

        #region Helpers
        private void AddXP(SkillType skill, int amount)
        {
            _skills[skill].AddXP(amount);
        }

        private int GetLevel(SkillType skill)
        {
            return _skills[skill].Level;
        }
        #endregion

        public string GetSkillsSummary()
        {
            return
                $"Niveau global: {OverallLevel}\n" +
                $"Endurance: {this[SkillType.Endurance].Level} ({this[SkillType.Endurance].XP} XP)\n" +
                $"Tir: {this[SkillType.Marksmanship].Level} ({this[SkillType.Marksmanship].XP} XP)\n" +
                $"Force: {this[SkillType.Strength].Level} ({this[SkillType.Strength].XP} XP)\n" +
                $"Tactique: {this[SkillType.Tactics].Level} ({this[SkillType.Tactics].XP} XP)\n" +
                $"Volonté: {this[SkillType.Willpower].Level} ({this[SkillType.Willpower].XP} XP)";
        }
    }
}
