using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
	public class ArmyCalculator
	{
		private static readonly Dictionary<UnitType, UnitType> CounterUnitTypes = new Dictionary<UnitType, UnitType>
		{
			[UnitType.Cavalry] = UnitType.Infantry,
			[UnitType.Infantry] = UnitType.Ranged,
			[UnitType.Ranged] = UnitType.Cavalry
		};
		private readonly Dictionary<Resource, int> resources;
		private readonly Dictionary<UnitType, int> myArmy, enemyArmy;
		private readonly StrategyMapInfo map;
		private static readonly Dictionary<UnitType, Dictionary<Resource, int>> unitCost = UnitsConstants.Current.UnitCost;
		private readonly List<UnitType> types;

		public ArmyCalculator(HommSensorData heroInfo, Location enemyLocation, StrategyMapInfo map)
		{
			resources = new Dictionary<Resource, int>(heroInfo.MyTreasury);
			myArmy = new Dictionary<UnitType, int>(heroInfo.MyArmy);
			enemyArmy = map[enemyLocation]?.NeutralArmy?.Army ?? map[enemyLocation]?.Garrison?.Army;
			if (enemyArmy == null)
				throw new ArgumentNullException($"No enemies at specified {nameof(enemyLocation)}");
			this.map = map;
			foreach (var units in myArmy)
			{
				if (!enemyArmy.ContainsKey(units.Key))
					enemyArmy[units.Key] = 0;
			}
			types = new List<UnitType>{UnitType.Militia};
			types.AddRange(enemyArmy.Where(x => x.Key != UnitType.Militia).OrderBy(x => x.Value).Select(x => CounterUnitTypes[x.Key]));
		}

		private bool IsWinner()
		{
			return Combat.Resolve(new ArmiesPair(myArmy, enemyArmy)).IsAttackerWin;
		}

		private int HowMuchCanBuy(UnitType unitType)
		{
			var availableCount = map.Dwellings.ContainsKey(unitType)
				? map.Dwellings[unitType].Sum(x => map[x].Dwelling.AvailableToBuyCount)
				: 0;
			return Math.Min(availableCount, unitCost[unitType].Min(x => resources[x.Key] / x.Value));
		}

		private void ManageArmy(UnitType unitType, int count, bool buy)
		{
			var c = (buy ? 1 : -1) * count;
			foreach (var resource in unitCost[unitType])
				resources[resource.Key] -= c * resource.Value;
			myArmy[unitType] += c;
		}

		public Dictionary<UnitType, int> GetArmyToWin()
		{
			return GetArmyToWinRec(0) ? myArmy : null;
		}

		private bool GetArmyToWinRec(int typeIndex)
		{
			if (typeIndex == types.Count) return IsWinner();
			var unitType = types[typeIndex];
			var availableCount = HowMuchCanBuy(unitType);
			var step = Math.Max(availableCount / 5, 1);
			for (var i = 0; step > 0; i+=step)
			{
				if (GetArmyToWinRec(typeIndex + 1)) return true;
				ManageArmy(unitType, step, true);
				step = Math.Min(availableCount - i, step);
			}
			ManageArmy(unitType, availableCount, false);
			return false;
		}
	}
}