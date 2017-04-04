using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
	public class ArmyCalculator
	{
		private readonly Dictionary<Resource, int> resources;
		private readonly Dictionary<UnitType, int> myArmy, enemyArmy;
		private readonly StrategyMapInfo map;
		private static readonly Dictionary<UnitType, Dictionary<Resource, int>> unitCost = UnitsConstants.Current.UnitCost;
		private static readonly List<UnitType> types = new List<UnitType>
		{
			UnitType.Militia, UnitType.Cavalry, UnitType.Ranged, UnitType.Infantry
		};

		public ArmyCalculator(HommSensorData heroInfo, Location enemyLocation, StrategyMapInfo map)
		{
			resources = new Dictionary<Resource, int>(heroInfo.MyTreasury);
			myArmy = new Dictionary<UnitType, int>(heroInfo.MyArmy);
			enemyArmy = map[enemyLocation].NeutralArmy?.Army ?? map[enemyLocation].Garrison?.Army;
			this.map = map;
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
			var c = buy ? -1 : 1 * count;
			foreach (var resource in unitCost[unitType])
				resources[resource.Key] += c * resource.Value;
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
			for (var i = 0; i <= availableCount; i++)
			{
				if (GetArmyToWinRec(typeIndex + 1)) return true;
				ManageArmy(unitType, 1, true);
			}
			ManageArmy(unitType, availableCount, false);
			return false;
		}
	}
}