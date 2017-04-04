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
	    private HommClient client;
	    private HommSensorData lastInfo;
		private HashSet<Location> enemies { get; }

		public Dictionary<UnitType, HashSet<Location>> Dwellings { get; }
		public Dictionary<Resource, HashSet<Location>> Mines { get; }

		public StrategyMapInfo(HommClient client)
		{
			client.OnSensorDataReceived += UpdateMapState;
			visited = new HashSet<Location>();
            mapObjects = new Dictionary<Location, MapObjectData>();
            enemies = new HashSet<Location>();
            Dwellings = new Dictionary<UnitType, HashSet<Location>>();
            Mines = new Dictionary<Resource, HashSet<Location>>();
	        this.client = client;
        }

        private void UpdateMapState(HommSensorData data)
        {
            foreach (var obj in data.Map.Objects)
            {
                var l = obj.Location.ToLocation();
                if (mapObjects.ContainsKey(l) && mapObjects[l] == obj) continue;
                mapObjects[l] = obj;
	            if (enemies.Contains(l)) enemies.Remove(l);
            }
	        lastInfo = data;
        }

		public HommSensorData InspectMap()
		{
			var location = lastInfo.Location.ToLocation();
			foreach (var direction in Directions)
			{
				var l = location.NeighborAt(direction.Key);
				var obj = this[l];
				if (obj == null) continue;
				AddObject(obj, l);
				if (visited.Contains(l) || !IsSafetyObject(obj)) continue;
				visited.Add(l);
				client.Move(direction.Key);
				InspectMap();
				client.Move(Directions[direction.Key]);
			}
			return lastInfo;
		}

		public Location GetRichestEnemyLocation()
		{
			return enemies.Select(enemy => Tuple.Create(InspectBeyondEnemy(enemy), enemy)).Argmax(x => x.Item1).Item2;
		}

		private int InspectBeyondEnemy(Location location)
		{
			var visitedCopy = new HashSet<Location>(visited);
			return InspectBeyondEnemyRec(location, visitedCopy);
		}

		private int InspectBeyondEnemyRec(Location location, HashSet<Location> visitedBeyondEnemy)
		{
			var result = GetMapObjectValue(this[location]);
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

		private int GetMapObjectValue(MapObjectData obj)
		{
			if (obj.ResourcePile != null)
				return obj.ResourcePile.Amount;
			if (obj.Mine != null && obj.NeutralArmy == null && obj.Garrison == null)
				return (int)(GameStrategy.Rules.CombatDuration - lastInfo.WorldCurrentTime);
			return 0;
		}

		private void AddObject(MapObjectData obj, Location location)
	    {
			if (IsEnemy(obj)) enemies.Add(location);
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

		public IEnumerable<Direction> GetPath(LocationInfo from, Location to)
		{
			var fromLocation = from.ToLocation();
			if (fromLocation == to) yield break;
			var map = new Dictionary<Location, int>();
			var q = new Queue<Location>();
			var visitedLocations = new HashSet<Location>();
			q.Enqueue(fromLocation);
			visitedLocations.Add(fromLocation);
			map[fromLocation] = 0;
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
			while (to != fromLocation)
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

        public MapObjectData this[Location l] => IsAvailableCell(l) && mapObjects.ContainsKey(l) ? mapObjects[l] : null;

        private bool IsInside(Location l)
        {
            return l.X >= 0 && l.X < lastInfo.Map.Width && l.Y >= 0 && l.Y < lastInfo.Map.Height;
        }

        private bool IsOpponentCastle(Location l)
        {
            return lastInfo.MyRespawnSide == "Left" && l.X == lastInfo.Map.Width - 1 && l.Y == lastInfo.Map.Height - 1 ||
                   lastInfo.MyRespawnSide == "Right" && l.X == 0 && l.Y == 0;
        }

        private bool IsAvailableCell(Location l)
        {
            return IsInside(l) && !IsOpponentCastle(l);
        }

        private bool IsEnemy(MapObjectData obj)
        {
            return obj.NeutralArmy != null || obj.Garrison != null && obj.Garrison.Owner != lastInfo.MyRespawnSide;
        }

		private bool IsSafetyObject(MapObjectData obj)
 		{
 			return obj == null || obj.NeutralArmy == null && obj.Wall == null &&
 				   (obj.Garrison?.Owner == null || obj.Garrison.Owner == lastInfo.MyRespawnSide);
 		}
    }
}