using System;
using System.Collections.Generic;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
	public class StrategyMapInfo
	{
		private bool[,] visited;
		private MapObjectData[,] objects;
		private readonly int height;
		private readonly int width;
		public static readonly Tuple<MapObjectData, bool> visitedCell = new Tuple<MapObjectData, bool>(null, true);

		public StrategyMapInfo(HommSensorData sensorData)
		{
			height = sensorData.Map.Height;
			width = sensorData.Map.Width;
			objects = new MapObjectData[height, width];
			visited = new bool[height, width];
			RefreshMapState(sensorData);
		}

		public void RefreshMapState(HommSensorData data)
		{
			foreach (var obj in data.Map.Objects)
			{
				if (objects[obj.Location.Y, obj.Location.X] == null || objects[obj.Location.Y, obj.Location.X] != obj)
					visited[obj.Location.Y, obj.Location.X] = false;
				objects[obj.Location.Y, obj.Location.X] = obj;
			}
		}

		public Tuple<MapObjectData, bool> this[Location location]
		{
			get
			{
				return IsOutside(location)
					? null
					: Tuple.Create(objects[location.Y, location.X], visited[location.Y, location.X]);
			}
			set { visited[location.Y, location.X] = value.Item2; }
		}

		private bool IsOutside(Location location)
		{
			return location.X < 0 || location.X >= width || location.Y < 0 || location.Y >= height;
		}

		public List<MapObjectData> FindWeakestEnemy(HommSensorData sensor)
		{
			bool[,] v = new bool[width, height];
			List<MapObjectData> enemies = new List<MapObjectData>();
			Queue<MapObjectData> data = new Queue<MapObjectData>();
			data.Enqueue(objects[sensor.Location.X, sensor.Location.Y]);
			while (data.Count != 0)
			{
				var curObject = data.Dequeue();
				v[curObject.Location.X, curObject.Location.Y] = true;
				foreach (var location in curObject.Location.ToLocation().Neighborhood)
				{
					if (IsOutside(location)) continue;
					var newObj = objects[location.X, location.Y];
					if (newObj.Wall != null || v[newObj.Location.X, newObj.Location.Y]) continue;
					if (newObj.NeutralArmy != null)
					{
						if (!v[newObj.Location.X, newObj.Location.Y])
						{
							enemies.Add(curObject);
							v[newObj.Location.X, newObj.Location.Y] = true;
							Console.WriteLine(newObj.Location);
						}
						continue;
					}
					data.Enqueue(newObj);
				}
			}
			return enemies;
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
			sensorData = client.Configurate(ip, port, cVarcTag, debugMap:true, spectacularView:false);
			map = new StrategyMapInfo(sensorData);
		}

		public void Execute()
		{
			CollectResourses();
		}

		private void CollectResourses()
		{
			var location = sensorData.Location.ToLocation();
			foreach (var direction in directions)
			{
				var obj = map[location.NeighborAt(direction.Key)];
				if (obj == null || obj.Item2 || !IsSafetyObject(obj.Item1)) continue;
				map[location] = StrategyMapInfo.visitedCell;
				sensorData = client.Move(direction.Key);
				CollectResourses();
				sensorData = client.Move(directions[direction.Key]);
			}
		}

		private bool IsSafetyObject(MapObjectData obj)
		{
			return obj == null || obj.Dwelling == null && obj.NeutralArmy == null && obj.Wall == null &&
			       obj.Garrison?.Owner == sensorData.MyRespawnSide;
		}
	}
}