using System;

namespace XCOM_3
{
    /// <summary>
    /// Système de progression et de compétences pour les unités
    /// </summary>
    public class UnitSkills
    {
        // Compétences et leurs XP
        public int EnduranceXP { get; private set; } = 0;      // Déplacement, esquive
        public int MarksmanshipXP { get; private set; } = 0;   // Tir à distance
        public int StrengthXP { get; private set; } = 0;       // Corps à corps, grenades
        public int TacticsXP { get; private set; } = 0;        // Utilisation de couvert
        public int WillpowerXP { get; private set; } = 0;      // Résistance aux dégâts
        
        // Niveaux de compétences (calculés à partir de l'XP)
        public int EnduranceLevel => CalculateLevel(EnduranceXP);
        public int MarksmanshipLevel => CalculateLevel(MarksmanshipXP);
        public int StrengthLevel => CalculateLevel(StrengthXP);
        public int TacticsLevel => CalculateLevel(TacticsXP);
        public int WillpowerLevel => CalculateLevel(WillpowerXP);
        
        // Niveau global de l'unité
        public int OverallLevel => (EnduranceLevel + MarksmanshipLevel + StrengthLevel + 
                                    TacticsLevel + WillpowerLevel) / 5;
        
        // XP nécessaire pour le prochain niveau de chaque compétence
        public int EnduranceXPToNext => XPForNextLevel(EnduranceXP);
        public int MarksmanshipXPToNext => XPForNextLevel(MarksmanshipXP);
        public int StrengthXPToNext => XPForNextLevel(StrengthXP);
        public int TacticsXPToNext => XPForNextLevel(TacticsXP);
        public int WillpowerXPToNext => XPForNextLevel(WillpowerXP);
        
        // Constantes de progression
        private const int BaseXPPerLevel = 100;
        private const float XPScaling = 1.5f; // Chaque niveau nécessite 50% de XP en plus
        
        /// <summary>
        /// Calcule le niveau actuel basé sur l'XP
        /// </summary>
        private int CalculateLevel(int xp)
        {
            int level = 0;
            int xpRequired = 0;
            
            while (xpRequired <= xp)
            {
                level++;
                xpRequired += (int)(BaseXPPerLevel * Math.Pow(XPScaling, level - 1));
            }
            
            return Math.Max(0, level - 1);
        }
        
        /// <summary>
        /// Calcule l'XP nécessaire pour atteindre le prochain niveau
        /// </summary>
        private int XPForNextLevel(int currentXP)
        {
            int currentLevel = CalculateLevel(currentXP);
            int xpForCurrentLevel = 0;
            
            for (int i = 0; i < currentLevel; i++)
            {
                xpForCurrentLevel += (int)(BaseXPPerLevel * Math.Pow(XPScaling, i));
            }
            
            int xpForNextLevel = xpForCurrentLevel + (int)(BaseXPPerLevel * Math.Pow(XPScaling, currentLevel));
            
            return xpForNextLevel - currentXP;
        }
        
        // ═══════════════════════════════════════════════════════════════════
        // MÉTHODES POUR GAGNER DE L'XP
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gagner de l'XP pour un déplacement
        /// </summary>
        public void GainMovementXP(int distance)
        {
            int xpGained = distance * 5; // 5 XP par case parcourue
            EnduranceXP += xpGained;
            Console.WriteLine($"[ENDURANCE] +{xpGained} XP (Distance: {distance})");
        }
        
        /// <summary>
        /// Gagner de l'XP pour un tir réussi
        /// </summary>
        public void GainShootingXP(bool hit, int distance, int damage)
        {
            int xpGained = 0;
            
            if (hit)
            {
                xpGained = 15 + (distance * 2) + (damage / 2); // Bonus pour distance et dégâts
                Console.WriteLine($"[MARKSMANSHIP] +{xpGained} XP (Tir réussi à {distance} cases, {damage} dmg)");
            }
            else
            {
                xpGained = 3; // Petit bonus même en cas de raté
                Console.WriteLine($"[MARKSMANSHIP] +{xpGained} XP (Tir raté)");
            }
            
            MarksmanshipXP += xpGained;
        }
        
        /// <summary>
        /// Gagner de l'XP pour avoir lancé une grenade
        /// </summary>
        public void GainGrenadeXP(int enemiesHit, int totalDamage)
        {
            int xpGained = 10 + (enemiesHit * 8) + (totalDamage / 3);
            StrengthXP += xpGained;
            Console.WriteLine($"[STRENGTH] +{xpGained} XP (Grenade: {enemiesHit} ennemis touchés, {totalDamage} dmg total)");
        }
        
        /// <summary>
        /// Gagner de l'XP pour avoir utilisé un couvert
        /// </summary>
        public void GainCoverXP()
        {
            int xpGained = 8;
            TacticsXP += xpGained;
            Console.WriteLine($"[TACTICS] +{xpGained} XP (Utilisation de couvert)");
        }
        
        /// <summary>
        /// Gagner de l'XP pour avoir survécu à des dégâts
        /// </summary>
        public void GainSurvivalXP(int damageTaken, bool survived)
        {
            int xpGained = (damageTaken / 5) + (survived ? 15 : 0);
            WillpowerXP += xpGained;
            Console.WriteLine($"[WILLPOWER] +{xpGained} XP (Survécu à {damageTaken} dégâts)");
        }
        
        /// <summary>
        /// Gagner de l'XP pour avoir tué un ennemi
        /// </summary>
        public void GainKillXP(string enemyClass)
        {
            // Bonus d'XP distribué entre les compétences
            int baseBonus = 20;
            
            MarksmanshipXP += baseBonus;
            WillpowerXP += baseBonus / 2;
            
            Console.WriteLine($"[KILL BONUS] Ennemi éliminé ({enemyClass}): +{baseBonus} Marksmanship, +{baseBonus/2} Willpower");
        }
        
        // ═══════════════════════════════════════════════════════════════════
        // BONUS ACCORDÉS PAR LES COMPÉTENCES
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Bonus de points de mouvement grâce à l'Endurance
        /// </summary>
        public int GetMovementBonus()
        {
            return EnduranceLevel / 3; // +1 PM tous les 3 niveaux
        }
        
        /// <summary>
        /// Bonus de précision grâce au Marksmanship
        /// </summary>
        public int GetAccuracyBonus()
        {
            return MarksmanshipLevel * 2; // +2% précision par niveau
        }
        
        /// <summary>
        /// Bonus de dégâts grâce à la Force
        /// </summary>
        public int GetDamageBonus()
        {
            return StrengthLevel; // +1 dégât par niveau
        }
        
        /// <summary>
        /// Bonus de défense grâce aux Tactics
        /// </summary>
        public int GetDefenseBonus()
        {
            return TacticsLevel * 2; // +2 armure par niveau
        }
        
        /// <summary>
        /// Bonus de PV max grâce au Willpower
        /// </summary>
        public int GetHealthBonus()
        {
            return WillpowerLevel * 5; // +5 PV par niveau
        }
        
        /// <summary>
        /// Obtenir un résumé formaté des compétences
        /// </summary>
        public string GetSkillsSummary()
        {
            return $"Niveau {OverallLevel}\n" +
                   $"Endurance: {EnduranceLevel} ({EnduranceXP} XP, {EnduranceXPToNext} pour next)\n" +
                   $"Tir: {MarksmanshipLevel} ({MarksmanshipXP} XP, {MarksmanshipXPToNext} pour next)\n" +
                   $"Force: {StrengthLevel} ({StrengthXP} XP, {StrengthXPToNext} pour next)\n" +
                   $"Tactique: {TacticsLevel} ({TacticsXP} XP, {TacticsXPToNext} pour next)\n" +
                   $"Volonté: {WillpowerLevel} ({WillpowerXP} XP, {WillpowerXPToNext} pour next)";
        }
        
        /// <summary>
        /// Constructeur de copie pour sauvegardes
        /// </summary>
        public UnitSkills(UnitSkills other)
        {
            if (other == null) return;
            
            EnduranceXP = other.EnduranceXP;
            MarksmanshipXP = other.MarksmanshipXP;
            StrengthXP = other.StrengthXP;
            TacticsXP = other.TacticsXP;
            WillpowerXP = other.WillpowerXP;
        }
        
        /// <summary>
        /// Constructeur par défaut
        /// </summary>
        public UnitSkills()
        {
            // Valeurs par défaut déjà initialisées
        }
    }
}
