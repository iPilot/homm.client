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
			this.client = client;
			//TODO: FIX PARAMETRES
			sensorData = client.Configurate(ip, port, cVarcTag, spectacularView: false, speedUp: true, timeLimit: 1500);
			map = new StrategyMapInfo(client, sensorData);
			Rules = new HommRules();
		}



        public void Execute()
        {
            sensorData = map.InspectMap(sensorData);
            var enemy = map.GetRichestEnemy();
            while (enemy != null)
            {
                var arm = GetArmyToWin(enemy);
                if (arm != null)
                {
                    foreach (var unit in arm)
                    {
                        if (unit.Value == 0) continue;
                        var location = map.Dwellings[unit.Key].Argmax(x => map[x].Dwelling.AvailableToBuyCount);
                        while (map[location].Dwelling.AvailableToBuyCount < unit.Value)
                        {
                            //в это месте он начинает бегать туда сюда по неизвестной причине
                            //видимо когда-то у Dwelling наступает предел восполнения
                            //и он больше не выдает юнитов
                            //или где-то просто не обновилась инфа :)
                            MoveTo(map.Mines[Resource.Gold].First());
                            MoveTo(location);
                        }
                        MoveTo(location);
                        sensorData = client.HireUnits(unit.Value);
                    }
                    MoveTo(enemy);
                    sensorData = map.InspectMap(sensorData);
                    enemy = map.GetRichestEnemy();
                    continue;
                }
                enemy = null;
            }
			sensorData = client.Wait(3);
        }

        private void MoveTo(Location target)
        {
	        foreach (var direction in map.GetPath(sensorData.Location, target))
		        sensorData = client.Move(direction);
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