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
            Location enemy = map.GetRichestEnemy();
            while (enemy != null)
            {
                var arm = GetArmyToWin(enemy);
                if (arm != null)
                {
                    foreach (var unit in arm)
                    {
                        if (unit.Value == 0) continue;
                        var location = map.Dwellings[unit.Key]
                            .Select(x => x).Argmax(x => map[x].Dwelling.AvailableToBuyCount);
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

            client.Wait(3);
            //foreach (var dwelling in map.Dwellings.SelectMany(x => x.Value))
            //{
            //	MoveTo(dwelling);
            //	client.Wait(2);
            //}
            //MoveTo(new Location(0, 0));
        }

        private void MoveTo(Location target)
		{
			var path = map.GetPath(sensorData.Location.ToLocation(), target);
			foreach (var direction in path)
			{
				sensorData = client.Move(direction);
			}
		}

        private Dictionary<UnitType, int> GetSumArmy(Dictionary<UnitType, int> needArmy)
        {
            var newArmy = new Dictionary<UnitType, int>();
            foreach (var unit in needArmy)
                newArmy[unit.Key] = sensorData.MyArmy.ContainsKey(unit.Key)
                ? needArmy[unit.Key] + sensorData.MyArmy[unit.Key] : needArmy[unit.Key];
            return newArmy;
        }

        private bool IsWinner(Dictionary<UnitType, int> myArmy, Dictionary<UnitType, int> enemyArmy)
        {
            return Combat.Resolve(new ArmiesPair(myArmy, enemyArmy)).IsAttackerWin;
        }

        private int HowMuchCanBuy(UnitType unit, Dictionary<Resource, int> myRes)
        {
            return myRes.Select(x =>
            {
                if (!UnitsConstants.Current.UnitCost[unit].ContainsKey(x.Key))
                    return int.MaxValue;
                return x.Value / UnitsConstants.Current.UnitCost[unit][x.Key];
            }).Min();
        }

        private Dictionary<Resource, int> BuyUnit(int count, UnitType unit, Dictionary<Resource, int> myRes)
        {
            var newResource = new Dictionary<Resource, int>();
            foreach (var res in myRes)
                if (!UnitsConstants.Current.UnitCost[unit].ContainsKey(res.Key))
                    newResource[res.Key] = res.Value;
                else
                    newResource[res.Key] = res.Value - UnitsConstants.Current.UnitCost[unit][res.Key] * count;
            return newResource;
        }

        private IEnumerable<Tuple<Dictionary<Resource, int>, int>> AlternativePurchases(UnitType unit, Dictionary<Resource, int> myRes, bool needToCount)
        {
            var canBuy = HowMuchCanBuy(unit, myRes);
            if (!needToCount)
            {
                yield return new Tuple<Dictionary<Resource, int>, int>(
                    myRes.Select(x => x).ToDictionary(k => k.Key, v => v.Value),
                    0
                    );
                yield break;
            }
            for (var i = 0; i <= canBuy; i++)
                yield return new Tuple<Dictionary<Resource, int>, int>(
                    BuyUnit(i, unit, myRes),
                    i
                    );
        }

        public Dictionary<UnitType, int> GetArmyToWin(Location enemyLocation)
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
			var power = 0;
			foreach (var unit in army)
			{
				power += unit.Value * UnitsConstants.Current.CombatPower[unit.Key];
			}
			return power;
		}
	}
}