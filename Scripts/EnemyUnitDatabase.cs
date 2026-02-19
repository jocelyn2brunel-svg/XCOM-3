using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3.Scripts
{
    public static class EnemyUnitDatabase
    {
        private static readonly Dictionary<string, EnemyTemplate> _enemies;

        static EnemyUnitDatabase()
        {
            _enemies = new Dictionary<string, EnemyTemplate>
            {
                { "Alien Grunt", 
                    new EnemyTemplate("Alien Grunt","Infantry","Franchi PA3",3) },
                
                { "Alien Sniper", 
                    new EnemyTemplate("Alien Sniper","Sniper","M2010 ESR",2) },
                
                { "Alien Heavy", 
                    new EnemyTemplate("Alien Heavy","Heavy","M16A1",2) },
                
                { "Alien Scout", 
                    new EnemyTemplate("Alien Scout","Scout","H&K MP5K",4) },
                
                { "Zombie", 
                    new EnemyTemplate("Zombie","Undead","Zombie Claws",2) }
            };
        }

        public static EnemyTemplate Get(string name)
        {
            if (!_enemies.ContainsKey(name))
                throw new Exception($"Enemy '{name}' not found in EnemyUnitDatabase.");

            return _enemies[name];
        }

        public static List<EnemyTemplate> GetAll()
        {
            return _enemies.Values.ToList();
        }
    }
}
