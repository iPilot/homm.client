using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    public class StrategyMapInfo
    {
        private Dictionary<Location, MapObjectData> mapObjects;
        private HashSet<Location> enemies;
        private Dictionary<UnitType, HashSet<Location>> dwellings;
        private Dictionary<Resource, HashSet<Location>> mines;
        private readonly int height;
        private readonly int width;
        private readonly string mySide;

        public StrategyMapInfo(HommSensorData sensorData)
        {
            height = sensorData.Map.Height;
            width = sensorData.Map.Width;
            mapObjects = new Dictionary<Location, MapObjectData>();
            enemies = new HashSet<Location>();
            dwellings = new Dictionary<UnitType, HashSet<Location>>();
            mines = new Dictionary<Resource, HashSet<Location>>();
            UpdateMapState(sensorData);
            mySide = sensorData.MyRespawnSide;
        }

        public void UpdateMapState(HommSensorData data)
        {
            foreach (var obj in data.Map.Objects)
            {
                var l = obj.Location.ToLocation();
                if (IsEnemy(obj)) enemies.Add(l);
                if (obj.Dwelling != null)
                {
                    if (!dwellings.ContainsKey(obj.Dwelling.UnitType))
                        dwellings.Add(obj.Dwelling.UnitType, new HashSet<Location>());
                    dwellings[obj.Dwelling.UnitType].Add(l);
                }
                if (obj.Mine != null)
                {
                    if (!mines.ContainsKey(obj.Mine.Resource))
                        mines.Add(obj.Mine.Resource, new HashSet<Location>());
                    mines[obj.Mine.Resource].Add(l);
                }
                if (mapObjects.ContainsKey(l) && mapObjects[l] == obj) continue;
                mapObjects[l] = obj;
            }
        }

        private List<Tuple<Location, int>> GetEnemiesPower()
        {
            var enemiesWithPower = new List<Tuple<Location, int>>();
            foreach (var location in enemies)
            {
                var army = mapObjects[location].NeutralArmy.Army;
                var power = 0;
                foreach (var unit in army)
                {
                    power = unit.Value * UnitsConstants.Current.CombatPower[unit.Key];
                }
                enemiesWithPower.Add(new Tuple<Location, int>(location, power));
            }
            return enemiesWithPower.OrderBy(x => x.Item2).ToList();
        }

        private int GetArmyPower(Dictionary<UnitType, int> army)
        {
            var power = 0;
            foreach (var unit in army)
            {
                power += unit.Value * UnitsConstants.Current.CombatPower[unit.Key];
            }
            return power;
        }

        private Dictionary<UnitType, int> GetArmyToWin(HommSensorData sensorData)
        {
            var enemiesWithPower = GetEnemiesPower();
            if (enemiesWithPower.Count == 0) return null;
            var currEnemyWithPower = enemiesWithPower.First();
            var currEnemy = mapObjects[currEnemyWithPower.Item1];
            var needArmy = new Dictionary<UnitType, int>
            {
                [UnitType.Militia] = 0,
                [UnitType.Ranged] = 0,
                [UnitType.Cavalry] = 0,
                [UnitType.Infantry] = 0
            };
            var myPower = GetArmyPower(sensorData.MyArmy);
            foreach (var unit in mapObjects[currEnemyWithPower.Item1].NeutralArmy.Army)
            {
                if (myPower > currEnemyWithPower.Item2) return needArmy;
                var heroUnits = sensorData.MyArmy.ContainsKey(unit.Key) ? sensorData.MyArmy[unit.Key] : 0;
                var enemyUnits = currEnemy.NeutralArmy.Army.ContainsKey(unit.Key) ? currEnemy.NeutralArmy.Army[unit.Key] : 0;
                while (heroUnits + needArmy[unit.Key] < enemyUnits)
                    needArmy[unit.Key] += 5;
            }
            return needArmy;
        }

        public MapObjectData this[Location l] => IsAvailableCell(l) && mapObjects.ContainsKey(l) ? mapObjects[l] : null;

        public bool IsInside(Location l)
        {
            return l.X >= 0 && l.X < width && l.Y >= 0 && l.Y < height;
        }

        private bool IsOpponentRespawn(Location l)
        {
            return mySide == "Left" && l.X == width - 1 && l.Y == height - 1 ||
                   mySide == "Right" && l.X == 0 && l.Y == 0;
        }

        private bool IsAvailableCell(Location l)
        {
            return IsInside(l) && !IsOpponentRespawn(l);
        }

        private bool IsEnemy(MapObjectData obj)
        {
            return obj.NeutralArmy != null || obj.Garrison != null && obj.Garrison.Owner != mySide;
        }
    }
}