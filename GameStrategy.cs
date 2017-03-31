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
		}

		public void Execute()
		{
			while (!sensorData.IsDead)
			{
				sensorData = CollectResourses();
			}
		}

		private HommSensorData CollectResourses()
		{
			var s = new Stack<Direction>();
			var location = sensorData.Location.ToLocation();
			foreach (var direction in directions)
			{
				var obj = location.NeighborAt(direction.Key);
			}


			throw new NotImplementedException();
		}
	}
}