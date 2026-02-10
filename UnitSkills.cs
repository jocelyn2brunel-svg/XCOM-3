using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public enum SkillType
    {
        Endurance,
        Marksmanship,
        Strength,
        Tactics,
        Willpower,

        // Nouveaux
        Perception,
        Intelligence,
        Stealth,
        Medical,
        Demolitions,
        Leadership,
        Resilience,
        WeaponHandling,
        Mobility
    }

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
        public void GainMovementXP(int distance) => 
            _skills[SkillType.Endurance].AddXP(distance * 5);

        public void GainShootingXP(bool hit, int distance, int damage)
        {
            _skills[SkillType.Marksmanship].AddXP(hit ? 15 + distance * 2 + damage / 2 : 3);
        }

        public void GainGrenadeXP(int enemiesHit, int totalDamage) =>
            _skills[SkillType.Strength].AddXP(10 + enemiesHit * 8 + totalDamage / 3);


        // Meilleure couverture avec le peu de cover auquel on a accès.
        public void GainCoverXP() => 
            _skills[SkillType.Tactics].AddXP(8);


        public void GainSurvivalXP(int damageTaken, bool survived) =>
            _skills[SkillType.Willpower].AddXP(damageTaken / 5 + (survived ? 15 : 0));


        public void GainKillXP(string enemyClass)
        {
            _skills[SkillType.Marksmanship].AddXP(20);
            _skills[SkillType.Willpower].AddXP(10);
        }

        // Détection / overwatch
        public void GainSpottingXP(int enemiesSpotted) =>
            _skills[SkillType.Perception].AddXP(10 + enemiesSpotted * 6);

        public void GainOverwatchXP(bool hit) =>
            _skills[SkillType.Perception].AddXP(hit ? 12 : 4);

        // Hack / tech
        public void GainHackXP(bool success, int difficulty) =>
            _skills[SkillType.Intelligence].AddXP(success ? 15 + difficulty * 5 : 5);

        // Furtivité
        public void GainStealthXP(int tilesMovedUndetected) =>
            _skills[SkillType.Stealth].AddXP(tilesMovedUndetected * 4);

        // Médical
        public void GainHealingXP(int hpRestored, bool savedLife) =>
            _skills[SkillType.Medical].AddXP(hpRestored + (savedLife ? 20 : 0));

        // Démolition
        public void GainDemolitionXP(int coverDestroyed, int totalDamage) =>
            _skills[SkillType.Demolitions].AddXP(10 + coverDestroyed * 8 + totalDamage / 2);

        // 🎒 Équipement lourd / surcharge
        public void GainHeavyCarryXP(int weight, int tilesMoved)
        {
            _skills[SkillType.Strength]
                .AddXP((weight / 5) * tilesMoved);
        }

        // 🔨 Mêlée / corps-à-corps
        public void GainMeleeXP(int damage, bool kill)
        {
            _skills[SkillType.Strength]
                .AddXP(10 + damage / 2 + (kill ? 15 : 0));
        }

        // Leadership
        public void GainLeadershipXP(int alliesNearby, bool squadSuccess)
        {
            _skills[SkillType.Leadership]
                .AddXP(alliesNearby * 5 + (squadSuccess ? 20 : 0));
        }

        // 🛡️ Resilience (ou Toughness)
        public void GainResilienceXP(int damageReduced)
        {
            _skills[SkillType.Resilience]
                .AddXP(damageReduced * 3);
        }
        public void GainWeaponHandlingXP(int shotsFired, bool movedBeforeShot)
        {
            _skills[SkillType.WeaponHandling]
                .AddXP(shotsFired * (movedBeforeShot ? 6 : 3));
        }

        // 🧭 Mobility(différent d’Endurance)
        public void GainMobilityXP(int shortMoves)
        {
            _skills[SkillType.Mobility].AddXP(shortMoves * 5);
        }



        // Bonuses
        public int GetMovementBonus() => 
            _skills[SkillType.Endurance].Level / 3;
        public int GetAccuracyBonus() => 
            _skills[SkillType.Marksmanship].Level * 2;
        public int GetDefenseBonus() => 
            _skills[SkillType.Tactics].Level * 2;
        public int GetHealthBonus() => 
            _skills[SkillType.Willpower].Level * 5;
        public int GetVisionBonus() =>
            _skills[SkillType.Perception].Level / 2;
        public int GetHackBonus() =>
            _skills[SkillType.Intelligence].Level * 3;
        public int GetStealthBonus() =>
            _skills[SkillType.Stealth].Level * 2;
        public int GetHealingBonus() =>
            _skills[SkillType.Medical].Level * 4;
        public int GetExplosionRadiusBonus() =>
            _skills[SkillType.Demolitions].Level / 3;
        public int GetCarryBonus() =>
            _skills[SkillType.Strength].Level * 2;
        public int GetMeleeHitBonus() =>
            _skills[SkillType.Strength].Level * 3;
        public int GetMoraleBonus() =>
            _skills[SkillType.Leadership].Level * 2;
        public int GetRecoilReduction() =>
            _skills[SkillType.WeaponHandling].Level * 2;
        public int GetReloadSpeedBonus() =>
            _skills[SkillType.WeaponHandling].Level * 2;



        public string GetSkillsSummary() =>
            $"Niveau global: {OverallLevel}\n" +
            string.Join("\n", _skills.Select(kvp => $"{kvp.Key}: {kvp.Value.Level} ({kvp.Value.XP} XP)"));
    }
}
