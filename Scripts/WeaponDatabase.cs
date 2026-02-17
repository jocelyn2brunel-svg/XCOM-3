using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Base de données complète d'armes réalistes
    /// Inspirée de Parasite Eve II, Terminator II, GoldenEye 64, etc.
    /// </summary>
    public static class WeaponDatabase
    {
        /// <summary>
        /// Retourne toutes les armes disponibles dans le jeu
        /// </summary>
        public static Dictionary<string, WeaponData> GetAllWeapons()
        {
            var weapons = new Dictionary<string, WeaponData>();

            // NOTE: 1 case = 5 pieds. Les portées ci-dessous sont alignées sur des portées efficaces réelles approximatives.

            // ═══════════════════════════════════════════════════════════════════
            // MÊLÉE
            // ═══════════════════════════════════════════════════════════════════
            
            weapons["PR-24 Tonfa"] = new WeaponData("PR-24 Tonfa", 
                damage: 20, accuracy: 95, range: 1, 
                weaponType: WeaponType.Melee, caliber: "Contondant");

            // ═══════════════════════════════════════════════════════════════════
            // PISTOLETS
            // ═══════════════════════════════════════════════════════════════════

            weapons["Beretta 92"] = new WeaponData("Beretta 92", 
                damage: 25, accuracy: 75, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm");

            weapons["Beretta 92FS"] = new WeaponData("Beretta 92FS", 
                damage: 26, accuracy: 76, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Terminator II");

            weapons["Beretta 93R"] = new WeaponData("Beretta 93R", 
                damage: 28, accuracy: 72, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["Luger P08"] = new WeaponData("Luger P08", 
                damage: 24, accuracy: 78, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["Sig Sauer P229"] = new WeaponData("Sig Sauer P229", 
                damage: 27, accuracy: 80, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["Glock 17"] = new WeaponData("Glock 17", 
                damage: 26, accuracy: 81, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Usage moderne");

            weapons["Calico M950"] = new WeaponData("Calico M950", 
                damage: 22, accuracy: 70, range: 39, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["Walther PPK"] = new WeaponData("Walther PPK", 
                damage: 20, accuracy: 82, range: 17, 
                weaponType: WeaponType.Pistol, caliber: ".380 ACP", 
                reference: "GoldenEye 64");

            weapons["Colt 1911"] = new WeaponData("Colt 1911", 
                damage: 30, accuracy: 78, range: 33, 
                weaponType: WeaponType.Pistol, caliber: ".45 ACP", 
                reference: "Terminator II");

            weapons["Detonics SpeedMaster"] = new WeaponData("Detonics SpeedMaster", 
                damage: 32, accuracy: 77, range: 17, 
                weaponType: WeaponType.Pistol, caliber: ".45 ACP", 
                reference: "Terminator II");

            weapons["Browning Hi-Power"] = new WeaponData("Browning Hi-Power", 
                damage: 26, accuracy: 79, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "9x19mm", 
                reference: "Terminator II");

            weapons["FN Five-seveN"] = new WeaponData("FN Five-seveN", 
                damage: 24, accuracy: 84, range: 33, 
                weaponType: WeaponType.Pistol, caliber: "5.7x28mm", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // REVOLVERS
            // ═══════════════════════════════════════════════════════════════════

            weapons["Korth Mongoose .44"] = new WeaponData("Korth Mongoose .44", 
                damage: 50, accuracy: 85, range: 33, 
                weaponType: WeaponType.Revolver, caliber: ".44 Magnum", 
                reference: "Parasite Eve II");

            // ═══════════════════════════════════════════════════════════════════
            // MITRAILLETTES (SMG)
            // ═══════════════════════════════════════════════════════════════════

            weapons["H&K MP5"] = new WeaponData("H&K MP5", 
                damage: 22, accuracy: 75, range: 131, 
                weaponType: WeaponType.SMG, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["H&K MP5K"] = new WeaponData("H&K MP5K", 
                damage: 20, accuracy: 70, range: 66, 
                weaponType: WeaponType.SMG, caliber: "9x19mm", 
                reference: "Parasite Eve II");

            weapons["H&K UMP45"] = new WeaponData("H&K UMP45", 
                damage: 27, accuracy: 72, range: 66, 
                weaponType: WeaponType.SMG, caliber: ".45 ACP", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // FUSILS D'ASSAUT
            // ═══════════════════════════════════════════════════════════════════

            weapons["M16A1"] = new WeaponData("M16A1", 
                damage: 35, accuracy: 80, range: 360, 
                weaponType: WeaponType.AssaultRifle, caliber: "5.56x45mm NATO", 
                reference: "Parasite Eve II");

            weapons["AK-47"] = new WeaponData("AK-47", 
                damage: 40, accuracy: 70, range: 230, 
                weaponType: WeaponType.AssaultRifle, caliber: "7.62x39mm");

            weapons["Type 56"] = new WeaponData("Type 56", 
                damage: 38, accuracy: 72, range: 230, 
                weaponType: WeaponType.AssaultRifle, caliber: "7.62x39mm");

            // ═══════════════════════════════════════════════════════════════════
            // CARABINES
            // ═══════════════════════════════════════════════════════════════════

            weapons["M4A1"] = new WeaponData("M4A1", 
                damage: 32, accuracy: 82, range: 328, 
                weaponType: WeaponType.Carbine, caliber: "5.56x45mm NATO", 
                reference: "Parasite Eve II");

            weapons["HK416"] = new WeaponData("HK416", 
                damage: 34, accuracy: 84, range: 328, 
                weaponType: WeaponType.Carbine, caliber: "5.56x45mm NATO", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // FUSILS DE COMBAT (BATTLE RIFLE)
            // ═══════════════════════════════════════════════════════════════════

            weapons["M14"] = new WeaponData("M14", 
                damage: 45, accuracy: 80, range: 328, 
                weaponType: WeaponType.BattleRifle, caliber: "7.62x51mm NATO");

            weapons["FN SCAR-H"] = new WeaponData("FN SCAR-H", 
                damage: 50, accuracy: 82, range: 394, 
                weaponType: WeaponType.BattleRifle, caliber: "7.62x51mm NATO", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // FUSILS À POMPE (SHOTGUN)
            // ═══════════════════════════════════════════════════════════════════

            weapons["Franchi PA3"] = new WeaponData("Franchi PA3", 
                damage: 60, accuracy: 65, range: 23, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Parasite Eve II");

            weapons["SP12"] = new WeaponData("SP12", 
                damage: 65, accuracy: 68, range: 23, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Parasite Eve II");

            weapons["AA-12"] = new WeaponData("AA-12", 
                damage: 70, accuracy: 70, range: 26, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Parasite Eve II");

            weapons["Winchester 1887"] = new WeaponData("Winchester 1887", 
                damage: 75, accuracy: 60, range: 20, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            weapons["Remington 870"] = new WeaponData("Remington 870", 
                damage: 68, accuracy: 72, range: 23, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            weapons["Mossberg 590"] = new WeaponData("Mossberg 590", 
                damage: 67, accuracy: 73, range: 23, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            weapons["Franchi SPAS-12"] = new WeaponData("Franchi SPAS-12", 
                damage: 72, accuracy: 68, range: 26, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            weapons["Franchi SPAS-15"] = new WeaponData("Franchi SPAS-15", 
                damage: 70, accuracy: 70, range: 26, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            weapons["Benelli M4"] = new WeaponData("Benelli M4", 
                damage: 69, accuracy: 74, range: 26, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Usage moderne");

            weapons["Ithaca 37"] = new WeaponData("Ithaca 37", 
                damage: 66, accuracy: 74, range: 23, 
                weaponType: WeaponType.Shotgun, caliber: "12 Gauge", 
                reference: "Terminator II");

            // ═══════════════════════════════════════════════════════════════════
            // MITRAILLEUSES (MACHINE GUN)
            // ═══════════════════════════════════════════════════════════════════

            weapons["M249 SAW"] = new WeaponData("M249 SAW", 
                damage: 30, accuracy: 65, range: 525, 
                weaponType: WeaponType.MachineGun, caliber: "5.56x45mm", 
                reference: "Parasite Eve II");

            weapons["PKM"] = new WeaponData("PKM", 
                damage: 36, accuracy: 62, range: 656, 
                weaponType: WeaponType.MachineGun, caliber: "7.62x54mmR", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // FUSILS DE TIREUR D'ÉLITE (DMR)
            // ═══════════════════════════════════════════════════════════════════

            weapons["MK 14 EBR"] = new WeaponData("MK 14 EBR", 
                damage: 55, accuracy: 88, range: 525, 
                weaponType: WeaponType.DMR, caliber: "7.62x51mm NATO");

            weapons["Marine Scout Rifle"] = new WeaponData("Marine Scout Rifle", 
                damage: 58, accuracy: 90, range: 590, 
                weaponType: WeaponType.DMR, caliber: "7.62x51mm NATO");

            weapons["Dragunov SVD"] = new WeaponData("Dragunov SVD", 
                damage: 60, accuracy: 89, range: 525, 
                weaponType: WeaponType.DMR, caliber: "7.62x54mmR", 
                reference: "Usage moderne");

            // ═══════════════════════════════════════════════════════════════════
            // FUSILS DE PRÉCISION (SNIPER RIFLE)
            // ═══════════════════════════════════════════════════════════════════

            weapons["M24 SWS"] = new WeaponData("M24 SWS", 
                damage: 80, accuracy: 95, range: 525, 
                weaponType: WeaponType.SniperRifle, caliber: "7.62x51mm NATO");

            weapons["M2010 ESR"] = new WeaponData("M2010 ESR", 
                damage: 85, accuracy: 96, range: 787, 
                weaponType: WeaponType.SniperRifle, caliber: ".300 Win Mag");

            weapons["MK 22 PSR"] = new WeaponData("MK 22 PSR", 
                damage: 90, accuracy: 97, range: 984, 
                weaponType: WeaponType.SniperRifle, caliber: ".338 Lapua");

            // ═══════════════════════════════════════════════════════════════════
            // LANCE-GRENADES
            // ═══════════════════════════════════════════════════════════════════

            weapons["Milkor MGL"] = new WeaponData("Milkor MGL", 
                damage: 100, accuracy: 75, range: 246, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "40mm Grenade");

            weapons["M79"] = new WeaponData("M79", 
                damage: 95, accuracy: 80, range: 246, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "40mm Grenade", 
                reference: "Terminator II");

            weapons["M320"] = new WeaponData("M320", 
                damage: 98, accuracy: 78, range: 262, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "40mm Grenade");

            weapons["MM1"] = new WeaponData("MM1", 
                damage: 105, accuracy: 72, range: 230, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "40mm Grenade", 
                reference: "Parasite Eve II");

            weapons["XM25"] = new WeaponData("XM25", 
                damage: 110, accuracy: 85, range: 459, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "25mm Airburst", 
                reference: "Battlefield 4");

            weapons["H&K HK69A1"] = new WeaponData("H&K HK69A1", 
                damage: 92, accuracy: 82, range: 230, 
                weaponType: WeaponType.GrenadeLauncher, caliber: "40mm Grenade");

            // ═══════════════════════════════════════════════════════════════════
            // LANCE-ROQUETTES
            // ═══════════════════════════════════════════════════════════════════

            weapons["M202A1 Flash"] = new WeaponData("M202A1 Flash", 
                damage: 150, accuracy: 70, range: 492, 
                weaponType: WeaponType.RocketLauncher, caliber: "66mm Incendiary", 
                reference: "Resident Evil 3");

            weapons["RPG-7"] = new WeaponData("RPG-7", 
                damage: 180, accuracy: 65, range: 328, 
                weaponType: WeaponType.RocketLauncher, caliber: "40mm Rocket");

            weapons["M72 LAW"] = new WeaponData("M72 LAW", 
                damage: 160, accuracy: 75, range: 131, 
                weaponType: WeaponType.RocketLauncher, caliber: "66mm Rocket", 
                reference: "Terminator II");

            // ═══════════════════════════════════════════════════════════════════
            // ZOMBIE
            // ═══════════════════════════════════════════════════════════════════

            weapons["Zombie Claws"] = new WeaponData(
                "Zombie Claws",
                damage: 30,
                accuracy: 75,
                range: 1,
                weaponType: WeaponType.Melee,
                caliber: "Natural"
);


            return weapons;
        }
    }

    /// <summary>
    /// Types d'armes étendus
    /// </summary>
    public enum WeaponType
    {
        Melee,
        Pistol,
        Revolver,
        SMG,
        AssaultRifle,
        Carbine,
        BattleRifle,
        Shotgun,
        MachineGun,
        DMR,              // Designated Marksman Rifle
        SniperRifle,
        GrenadeLauncher,
        RocketLauncher
    }


    public class WeaponData
    {
        public string Name;
        public int Damage, Accuracy, Range;

        // ✅ NOUVEAUX CHAMPS (optionnels - peuvent rester vides)
        public WeaponType Type;
        public string Caliber;
        public string Reference;
        public float WeightLbs;

        // Constructeur de base (pour vos armes existantes - compatibilité totale)
        public WeaponData(string name, int damage, int accuracy, int range)
        {
            Name = name;
            Damage = damage;
            Accuracy = accuracy;
            Range = range;

            // Valeurs par défaut pour les nouveaux champs
            Type = WeaponType.AssaultRifle;
            Caliber = "Unknown";
            Reference = "";
            WeightLbs = GetDefaultWeightLbs(Type);
        }

        // ✅ NOUVEAU : Constructeur étendu (pour les nouvelles armes)
        public WeaponData(string name, int damage, int accuracy, int range,
                         WeaponType weaponType, string caliber, string reference = "", float weightLbs = 0f)
        {
            Name = name;
            Damage = damage;
            Accuracy = accuracy;
            Range = range;
            Type = weaponType;
            Caliber = caliber;
            Reference = reference;
            WeightLbs = weightLbs > 0f ? weightLbs : GetDefaultWeightLbs(Type);
        }

        private static float GetDefaultWeightLbs(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Melee => 2.0f,
                WeaponType.Pistol => 2.2f,
                WeaponType.Revolver => 2.7f,
                WeaponType.SMG => 6.5f,
                WeaponType.AssaultRifle => 7.8f,
                WeaponType.Carbine => 7.1f,
                WeaponType.BattleRifle => 9.3f,
                WeaponType.Shotgun => 8.0f,
                WeaponType.MachineGun => 18.0f,
                WeaponType.DMR => 10.5f,
                WeaponType.SniperRifle => 12.8f,
                WeaponType.GrenadeLauncher => 13.5f,
                WeaponType.RocketLauncher => 20.0f,
                _ => 7.5f
            };
        }
    }

}
