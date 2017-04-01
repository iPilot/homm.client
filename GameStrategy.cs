using System;
using System.Collections.Generic;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
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
			client.Wait(25);
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
				map.UpdateMapState(sensorData);
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