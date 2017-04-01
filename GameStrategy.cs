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
			RefreshMapState(sensorData);
			mySide = sensorData.MyRespawnSide;
		}

		public void RefreshMapState(HommSensorData data)
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
	        return enemiesWithPower.OrderBy(x => x.Item2).ToList();
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

	public class GameStrategy
	{
		private HommClient client;
		private HommSensorData sensorData;
		private StrategyMapInfo map;
		private Dictionary<Direction, Direction> directions = new Dictionary<Direction, Direction>
		{
			{Direction.LeftUp, Direction.RightDown},
			{Direction.Up, Direction.Down},
			{Direction.RightUp, Direction.LeftDown },
			{Direction.RightDown, Direction.LeftUp},
			{Direction.Down, Direction.Up},
			{Direction.LeftDown, Direction.RightUp}
		};

		public GameStrategy(HommClient client, string ip, int port, Guid cVarcTag)
		{
			this.client = client;
			//TODO: FIX PARAMETRES
			sensorData = client.Configurate(ip, port, cVarcTag, spectacularView: false, speedUp: true);
			map = new StrategyMapInfo(sensorData);
		}

		public void Execute()
		{
			InspectMap();
		}

		private void InspectMap()
		{
			var visited = new HashSet<Location> {sensorData.Location.ToLocation()};
			InspectMapRec(visited);
		}

		private void InspectMapRec(HashSet<Location> visited)
		{
			var location = sensorData.Location.ToLocation();
			foreach (var direction in directions)
			{
				var l = location.NeighborAt(direction.Key);
				var obj = map[l];
				if (obj == null || visited.Contains(l) || !IsSafetyObject(obj)) continue;
				visited.Add(l);
				sensorData = client.Move(direction.Key);
				map.RefreshMapState(sensorData);
				InspectMapRec(visited);
				sensorData = client.Move(directions[direction.Key]);
			}
        }
       
        



		//public List<MapObjectData> FindEnemies(HommSensorData sensor)
		//{
		//	bool[,] v = new bool[height, width];
		//	List<MapObjectData> enemies = new List<MapObjectData>();
		//	Queue<MapObjectData> data = new Queue<MapObjectData>();
		//	data.Enqueue(objects[sensor.Location.X, sensor.Location.Y]);
		//	while (data.Count != 0)
		//	{
		//		var curObject = data.Dequeue();
		//		v[curObject.Location.X, curObject.Location.Y] = true;
		//		foreach (var location in curObject.Location.ToLocation().Neighborhood)
		//		{
		//			if (IsOutside(location)) continue;
		//			var newObj = objects[location.X, location.Y];
		//			if (newObj.Wall != null || v[newObj.Location.X, newObj.Location.Y]) continue;
		//			if (newObj.NeutralArmy != null)
		//			{
		//				if (!v[newObj.Location.X, newObj.Location.Y])
		//				{
		//					enemies.Add(curObject);
		//					v[newObj.Location.X, newObj.Location.Y] = true;
		//					Console.WriteLine(newObj.Location);
		//				}
		//				continue;
		//			}
		//			data.Enqueue(newObj);
		//		}
		//	}
		//	return enemies;
		//}

		private bool IsSafetyObject(MapObjectData obj)
		{
			return obj == null || obj.NeutralArmy == null && obj.Wall == null &&
			       (obj.Garrison?.Owner == null || obj.Garrison.Owner == sensorData.MyRespawnSide);
		}

	}
}