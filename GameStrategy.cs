using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
	public class GameStrategy
	{
		private HommClient client;
		private HommSensorData sensorData;
		private StrategyMapInfo map;
		public static HommRules Rules = new HommRules();

		public GameStrategy(HommClient client, string ip, int port, Guid cVarcTag)
		{
			Rules = new HommRules();
			this.client = client;
			map = new StrategyMapInfo(client);
			//TODO: FIX PARAMETRES
			sensorData = client.Configurate(ip, port, cVarcTag, spectacularView: false, speedUp: true, timeLimit: 1500);
		}

        public void Execute()
        {
            sensorData = map.InspectMap();
            var enemy = map.GetRichestEnemyLocation();
            while (enemy != null)
            {
				var arm = new ArmyCalculator(sensorData, enemy, map).GetArmyToWin();
	            if (arm == null) continue;
                foreach (var unit in arm.Where(x => x.Value > sensorData.MyArmy[x.Key]))
                {
	                foreach (var d in GetDwellings(unit.Key))
	                {
						MoveTo(d.Item1);
						sensorData = client.HireUnits(Math.Min(d.Item2, unit.Value - sensorData.MyArmy[unit.Key]));
		                if (sensorData.MyArmy[unit.Key] == unit.Value) break;
					}
                }
                MoveTo(enemy);
                sensorData = map.InspectMap();
                enemy = map.GetRichestEnemyLocation();
            }
			sensorData = client.Wait(3);
        }

        private void MoveTo(Location target)
        {
	        foreach (var direction in map.GetPath(sensorData.Location, target))
		        sensorData = client.Move(direction);
        }

		private IEnumerable<Tuple<Location, int>> GetDwellings(UnitType unitType)
		{
			return map.Dwellings[unitType]
				.Select(x => Tuple.Create(x, map[x].Dwelling.AvailableToBuyCount))
				.OrderByDescending(x => x.Item2);
		}

        private Dictionary<UnitType, int> GetSumArmy(Dictionary<UnitType, int> needArmy)
        {
	        return needArmy.ToDictionary(x => x.Key, y => y.Value + sensorData.MyArmy[y.Key]);
        }

        private bool IsWinner(Dictionary<UnitType, int> myArmy, Dictionary<UnitType, int> enemyArmy)
        {
            return Combat.Resolve(new ArmiesPair(myArmy, enemyArmy)).IsAttackerWin;
        }

        private int HowMuchCanBuy(UnitType unit, Dictionary<Resource, int> myRes)
        {
			return UnitsConstants.Current.UnitCost[unit].Min(x => myRes[x.Key] / x.Value);
		}

        private Dictionary<Resource, int> BuyUnit(int count, UnitType unit, Dictionary<Resource, int> myRes)
        {
            var newResource = new Dictionary<Resource, int>(myRes);
            foreach (var res in UnitsConstants.Current.UnitCost[unit])
				newResource[res.Key] -= res.Value * count;
            return newResource;
        }

        private IEnumerable<Tuple<Dictionary<Resource, int>, int>> AlternativePurchases(UnitType unit, Dictionary<Resource, int> myRes, bool needToCount)
        {
            var canBuy = HowMuchCanBuy(unit, myRes);
	        if (!needToCount)
		        yield return Tuple.Create(myRes.ToDictionary(k => k.Key, v => v.Value), 0);
	        else
				for (var i = 0; i <= canBuy; i++)
			        yield return Tuple.Create(BuyUnit(i, unit, myRes), i);
        }

        private Dictionary<UnitType, int> GetArmyToWin(Location enemyLocation)
        {
            var enemy = map[enemyLocation];
            var enemyArmy = enemy.Garrison == null ? enemy.NeutralArmy.Army : enemy.Garrison.Army;
            var myRes = sensorData.MyTreasury;
            var myArmy = sensorData.MyArmy;
            var needArmy = new Dictionary<UnitType, int>
            {
                [UnitType.Militia] = 0,
                [UnitType.Cavalry] = 0,
                [UnitType.Ranged] = 0,
                [UnitType.Infantry] = 0
            };
            var types = new List<UnitType>
			{
				UnitType.Militia, UnitType.Cavalry, UnitType.Ranged, UnitType.Infantry
			};
            var needToCount = types.Select(type => map.Dwellings.ContainsKey(type)).ToList();
	        foreach (var mil in AlternativePurchases(types[0], myRes, needToCount[0]))
            {
                needArmy[types[0]] = mil.Item2;
                if (IsWinner(GetSumArmy(needArmy), enemyArmy)) return needArmy;
                foreach (var cav in AlternativePurchases(types[1], mil.Item1, needToCount[1]))
                {
                    needArmy[types[1]] = cav.Item2;
                    if (IsWinner(GetSumArmy(needArmy), enemyArmy)) return needArmy;
                    foreach (var ran in AlternativePurchases(types[2], mil.Item1, needToCount[2]))
                    {
                        needArmy[types[2]] = ran.Item2;
                        if (IsWinner(GetSumArmy(needArmy), enemyArmy)) return needArmy;
                        foreach (var inf in AlternativePurchases(types[3], mil.Item1, needToCount[3]))
                        {
                            needArmy[types[3]] = inf.Item2;
                            if (IsWinner(GetSumArmy(needArmy), enemyArmy)) return needArmy;
                        }
                    }
                }
            }
            return null;
        }

		private int GetArmyPower(Dictionary<UnitType, int> army)
		{
			return army.Sum(unit => unit.Value * UnitsConstants.Current.CombatPower[unit.Key]);
		}
	}
}