﻿using System;
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
			sensorData = client.Configurate(ip, port, cVarcTag, spectacularView: false, speedUp: true, timeLimit: 300);
			map = new StrategyMapInfo(client, sensorData);
			Rules = new HommRules();
		}

		public void Execute()
		{
			sensorData = map.InspectMap(sensorData);
			client.Wait(3);
			//foreach (var dwelling in map.Dwellings.SelectMany(x => x.Value))
			//{
			//	MoveTo(dwelling);
			//	client.Wait(2);
			//}
			MoveTo(map.Dwellings[UnitType.Cavalry].First());
			//client.Wait(3);
			sensorData = client.HireUnits(map[sensorData.Location.ToLocation()].Dwelling.AvailableToBuyCount);
			//client.Wait(3);
			MoveTo(new Location(12, 2));
			sensorData = map.InspectMap(sensorData);
			MoveTo(new Location(0, 0));
		}

		private void MoveTo(Location target)
		{
			var path = map.GetPath(sensorData.Location.ToLocation(), target);
			foreach (var direction in path)
			{
				sensorData = client.Move(direction);
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
	}
}