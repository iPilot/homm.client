using System;
using System.Collections.Generic;
using System.Linq;
using HoMM;
using HoMM.ClientClasses;

namespace Homm.Client
{
    public class StrategyMapInfo
    {
		public static Dictionary<Direction, Direction> Directions = new Dictionary<Direction, Direction>
		{
 			{Direction.LeftUp, Direction.RightDown},
 			{Direction.Up, Direction.Down},
 			{Direction.RightUp, Direction.LeftDown },
 			{Direction.RightDown, Direction.LeftUp},
 			{Direction.Down, Direction.Up},
 			{Direction.LeftDown, Direction.RightUp}
 		};

        private Dictionary<Location, MapObjectData> mapObjects;
		private HashSet<Location> visited;
		private readonly int height;
        private readonly int width;
        private readonly string mySide;
	    private HommClient client;
	    private double WorldCurrentTime;

		public HashSet<Location> Enemies { get; }
		public Dictionary<UnitType, HashSet<Location>> Dwellings { get; }
		public Dictionary<Resource, HashSet<Location>> Mines { get; }

		public StrategyMapInfo(HommClient client, HommSensorData sensorData)
        {
            height = sensorData.Map.Height;
            width = sensorData.Map.Width;
			visited = new HashSet<Location>();
            mapObjects = new Dictionary<Location, MapObjectData>();
            Enemies = new HashSet<Location>();
            Dwellings = new Dictionary<UnitType, HashSet<Location>>();
            Mines = new Dictionary<Resource, HashSet<Location>>();
            UpdateMapState(sensorData);
            mySide = sensorData.MyRespawnSide;
	        this.client = client;
	        WorldCurrentTime = sensorData.WorldCurrentTime;
			InspectMap(sensorData);
        }

        public void UpdateMapState(HommSensorData data)
        {
            foreach (var obj in data.Map.Objects)
            {
                var l = obj.Location.ToLocation();
                if (mapObjects.ContainsKey(l) && mapObjects[l] == obj) continue;
                mapObjects[l] = obj;
            }
	        WorldCurrentTime = data.WorldCurrentTime;
        }

		public void InspectMap(HommSensorData sensorData)
		{
			var location = sensorData.Location.ToLocation();
			UpdateMapState(sensorData);
			foreach (var direction in Directions)
			{
				var l = location.NeighborAt(direction.Key);
				var obj = this[l];
				if (obj == null) continue;
				AddObject(obj, l);
				if (visited.Contains(l) || !IsSafetyObject(obj)) continue;
				visited.Add(l);
				InspectMap(client.Move(direction.Key));
				client.Move(Directions[direction.Key]);
			}
		}

		public Location GetRichestEnemy()
		{
			return Enemies.Select(enemy => Tuple.Create(InspectBeyondEnemy(enemy), enemy)).Argmax(x => x.Item1).Item2;
		}

		private int InspectBeyondEnemy(Location location)
		{
			var visitedCopy = new HashSet<Location>(visited);
			return InspectBeyondEnemyRec(location, visitedCopy);
		}

		private int InspectBeyondEnemyRec(Location location, HashSet<Location> visitedBeyondEnemy)
		{
			var result = GetCellValue(this[location]);
			foreach (var direction in Directions)
			{
				var l = location.NeighborAt(direction.Key);
				var obj = this[l];
				if (obj == null || visitedBeyondEnemy.Contains(l) || !IsSafetyObject(obj)) continue;
				visitedBeyondEnemy.Add(l);
				result += InspectBeyondEnemyRec(l, visitedBeyondEnemy);
			}
			return result;
		}

		public int GetCellValue(MapObjectData obj)
		{
			if (obj.ResourcePile != null)
				return obj.ResourcePile.Amount;
			if (obj.Mine != null && obj.NeutralArmy == null && obj.Garrison == null)
				return (int)(GameStrategy.Rules.CombatDuration - WorldCurrentTime);
			return 0;
		}

		private void AddObject(MapObjectData obj, Location location)
	    {
			if (IsEnemy(obj)) Enemies.Add(location);
			if (obj.Dwelling != null)
			{
				if (!Dwellings.ContainsKey(obj.Dwelling.UnitType))
					Dwellings.Add(obj.Dwelling.UnitType, new HashSet<Location>());
				Dwellings[obj.Dwelling.UnitType].Add(location);
			}
			if (obj.Mine != null)
			{
				if (!Mines.ContainsKey(obj.Mine.Resource))
					Mines.Add(obj.Mine.Resource, new HashSet<Location>());
				Mines[obj.Mine.Resource].Add(location);
			}
		}

		public IEnumerable<Direction> GetPath(Location from, Location to)
		{
			if (from == to) yield break;
			var map = new Dictionary<Location, int>();
			var q = new Queue<Location>();
			var visitedLocations = new HashSet<Location>();
			q.Enqueue(from);
			visitedLocations.Add(from);
			map[from] = 0;
			while (q.Count > 0)
			{
				var location = q.Dequeue();
				foreach (var direction in Directions)
				{
					var objLocation = location.NeighborAt(direction.Key);
					if (objLocation == to)
					{
						map[objLocation] = map[location] + 1;
						q.Clear();
						break;
					}
					var obj = this[objLocation];
					if (obj == null || visitedLocations.Contains(objLocation) || !IsSafetyObject(obj)) continue;
					q.Enqueue(objLocation);
					map[objLocation] = map[location] + 1;
					visitedLocations.Add(objLocation);
				}
			}
			if (!map.ContainsKey(to)) yield break;
			var result = new List<Direction>(map[to] + 1);
			while (to != from)
			{
				foreach (var direction in Directions)
				{
					var prevLocation = to.NeighborAt(direction.Key);
					if (!map.ContainsKey(prevLocation) || map[prevLocation] + 1 != map[to]) continue;
					result.Add(direction.Value);
					to = prevLocation;
				}
			}
			for (var i = result.Count - 1; i >= 0; i--)
				yield return result[i];
		}

        private int GetArmyPower(Dictionary<UnitType, int> army)
        {
            var power = 0;
            foreach (var unit in army)
            {
                power += unit.Value * UnitsConstants.Current.CombatPower[unit.Key];
            }
            return power;
        }

        public MapObjectData this[Location l] => IsAvailableCell(l) && mapObjects.ContainsKey(l) ? mapObjects[l] : null;

        public bool IsInside(Location l)
        {
            return l.X >= 0 && l.X < width && l.Y >= 0 && l.Y < height;
        }

        private bool IsOpponentCastle(Location l)
        {
            return mySide == "Left" && l.X == width - 1 && l.Y == height - 1 ||
                   mySide == "Right" && l.X == 0 && l.Y == 0;
        }

        private bool IsAvailableCell(Location l)
        {
            return IsInside(l) && !IsOpponentCastle(l);
        }

        private bool IsEnemy(MapObjectData obj)
        {
            return obj.NeutralArmy != null || obj.Garrison != null && obj.Garrison.Owner != mySide;
        }

		private bool IsSafetyObject(MapObjectData obj)
 		{
 			return obj == null || obj.NeutralArmy == null && obj.Wall == null &&
 				   (obj.Garrison?.Owner == null || obj.Garrison.Owner == mySide);
 		}
    }
}