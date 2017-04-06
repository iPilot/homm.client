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
			Rules = new HommRules();
			this.client = client;
			map = new StrategyMapInfo(client);
			//TODO: FIX PARAMETRES
			sensorData = client.Configurate(ip, port, cVarcTag, spectacularView: false, speedUp: true, timeLimit: 1500);
		}

        public void Execute()
        {
            sensorData = map.InspectMap();
            var enemy = map.GetRichestEnemyLocation();
            while (enemy != null)
            {
				var arm = new ArmyCalculator(sensorData, enemy, map).GetArmyToWin();
	            if (arm == null) continue;
                foreach (var unit in arm.Where(x => x.Value > sensorData.MyArmy[x.Key]))
                {
	                foreach (var d in GetDwellings(unit.Key))
	                {
						MoveTo(d.Item1);
						sensorData = client.HireUnits(Math.Min(d.Item2, unit.Value - sensorData.MyArmy[unit.Key]));
		                if (sensorData.MyArmy[unit.Key] == unit.Value) break;
					}
                }
                MoveTo(enemy);
                sensorData = map.InspectMap();
                enemy = map.GetRichestEnemyLocation();
            }
			sensorData = client.Wait(3);
        }

        private void MoveTo(Location target)
        {
	        foreach (var direction in map.GetPath(sensorData.Location, target))
		        sensorData = client.Move(direction);
        }

		private IEnumerable<Tuple<Location, int>> GetDwellings(UnitType unitType)
		{
			return map.Dwellings[unitType]
				.Select(x => Tuple.Create(x, map[x].Dwelling.AvailableToBuyCount))
				.OrderByDescending(x => x.Item2);
		}
	}
}