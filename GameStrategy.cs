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

		public StrategyMapInfo(HommSensorData sensorData)
		{
			height = sensorData.Map.Height;
			width = sensorData.Map.Width;
			objects = new MapObjectData[height, width];
			visited = new bool[height, width];
		}

		public void RefreshMapState(HommSensorData data)
		{
			foreach (var obj in data.Map.Objects)
			{
				if (objects[obj.Location.X, obj.Location.Y] == null || objects[obj.Location.X, obj.Location.Y] != obj)
					visited[obj.Location.X, obj.Location.Y] = false;
				objects[obj.Location.X, obj.Location.Y] = obj;
			}
		}

		public Tuple<MapObjectData, bool> this[Location location] => 
			IsOutside(location) 
			? null 
			: Tuple.Create(objects[location.X, location.Y], visited[location.X, location.Y]);

		private bool IsOutside(Location location)
		{
			return location.X < 0 || location.X >= width || location.Y < 0 || location.Y >= height;
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
			sensorData = client.Configurate(ip, port, cVarcTag);
			map = new StrategyMapInfo(sensorData);
		}

		public void Execute()
		{
			//while (!sensorData.IsDead)
			//{
				sensorData = CollectResourses();
			//}
		}

		private HommSensorData CollectResourses()
		{
			var s = new Stack<Direction>();
			foreach (var location in sensorData.Location.ToLocation().Neighborhood)
			{
				var obj = map[location];
				switch (obj)
				{
					
				}
			}

			throw new NotImplementedException();
		}
	}
}