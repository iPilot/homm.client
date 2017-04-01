using System;
using System.Collections.Generic;
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
					power = unit.Value*UnitsConstants.Current.CombatPower[unit.Key];
				}
				enemiesWithPower.Add(new Tuple<Location, int>(location, power));
			}
			return enemiesWithPower;
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