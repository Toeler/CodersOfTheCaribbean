using System;
using System.Collections.Generic;
using System.Linq;

namespace CodersOfTheCaribbean {
	public enum Orientation {
		Right,
		UpRight,
		UpLeft,
		Left,
		DownLeft,
		DownRight
	};

	public class Coordinate {
		private static readonly int[][] DIRECTIONS_EVEN= {
			new[] {  1,  0 },
			new[] {  0, -1 },
			new[] { -1, -1 },
			new[] { -1,  0 },
			new[] { -1,  1 },
			new[] {  0,  1 }
		};
		private static readonly int[][] DIRECTIONS_ODD = {
			new[] {  1,  0 },
			new[] {  1, -1 },
			new[] {  0, -1 },
			new[] { -1,  0 },
			new[] {  0,  1 },
			new[] {  1,  1 }
		};
		private static readonly IDictionary<string, int> DISTANCE_CACHE = new Dictionary<string, int>(); 
		private static readonly IDictionary<string, Coordinate> NEIGHBOR_CACHE = new Dictionary<string, Coordinate>(); 
		private static readonly IDictionary<string, double> ANGLE_CACHE = new Dictionary<string, double>(); 

		public int X { get; }
		public int Y { get; }

		public Coordinate(int x, int y) {
			X = x;
			Y = y;
		}

		public Coordinate(Coordinate coordinate) : this(coordinate.X, coordinate.Y) {
		}

		public double Angle(Coordinate targetCoordinate) {
			var key = $"{this}|{targetCoordinate}";
			double result;
			if(ANGLE_CACHE.TryGetValue(key, out result)) {
				return result;
			}
			var dy = (targetCoordinate.Y - Y)*Math.Sqrt(3)/2;
			var dx = targetCoordinate.X - X + ((Y - targetCoordinate.Y) & 1)*0.5;
			var angle = -Math.Atan2(dy, dx)*3/Math.PI;
			if (angle < 0) {
				angle += 6;
			} else if (angle >= 6) {
				angle -= 6;
			}
			result = angle;
			ANGLE_CACHE.Add(key, result);
			return result;
		}

		public Coordinate Neighbor(Orientation orientation) {
			var key = $"{this}|{(int)orientation}";
			Coordinate result;
			if(NEIGHBOR_CACHE.TryGetValue(key, out result)) {
				return result;
			}
			int newX, newY;
			if (Y%2 == 1) {
				var directions = DIRECTIONS_ODD[(int)orientation];
				newX = (X + directions[0]);
				newY = (Y + directions[1]);
			} else {
				var directions = DIRECTIONS_EVEN[(int)orientation];
				newX = (X + directions[0]);
				newY = (Y + directions[1]);
			}
			result = new Coordinate(newX, newY);
			NEIGHBOR_CACHE.Add(key, result);
			return result;
		}

		/*private IEnumerable<Coordinate> Neighbors() {
			return Enumerable.Range(0, 6).Select(i => Neighbor((Orientation)i)).Where(c => c.IsInsideMap());
		}

		public IEnumerable<Coordinate> PathTo(GameState gameState, Coordinate target) {
			// TASKS:
			// 1) Add turning to path calculation
			// 2) Add Bow and Stern to calculation
			// 3) Avoid mines bowing up next to us

			IDictionary<Coordinate, Coordinate> cameFrom = new Dictionary<Coordinate, Coordinate>();
			IDictionary<Coordinate, double> costSoFar = new Dictionary<Coordinate, double>();

			var frontier = new PriorityQueue<PriorityQueueItem<Coordinate>>();
			frontier.Enqueue(new PriorityQueueItem<Coordinate>(this, 0));

			cameFrom[this] = this;
			costSoFar[this] = 0;

			while (frontier.Count() > 0) {
				var current = frontier.Dequeue();
				var currentItem = current.Item;

				if (currentItem.Equals(target)) {
					break;
				}

				foreach (var next in currentItem.Neighbors()) {
					var newCost = costSoFar[currentItem] + 1;
					var angle = (Orientation)currentItem.Angle(next);
					//var nextBow = next.Neighbor(angle);
					var nextStern = currentItem.Neighbor(angle.Opposite());

					if (/*!nextBow.IsInsideMap() || !nextStern.IsInsideMap()) {
						continue;
					}

					if (gameState.Mines.FirstOrDefault(mine => mine.Position.Equals(next)/* || mine.Position.Equals(nextBow) || mine.Position.Equals(nextStern)) != null) {
						newCost += 10;
					}

					if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next]) {
						costSoFar[next] = newCost;
						var priority = newCost + next.DistanceTo(target);
						frontier.Enqueue(new PriorityQueueItem<Coordinate>(next, priority));
						cameFrom[next] = currentItem;
					}
				}
			}

			var result = new List<Coordinate>();
			var pathItem = target;
			while (pathItem != null && costSoFar[pathItem] != 0) {
				result.Add(pathItem);
				cameFrom.TryGetValue(pathItem, out pathItem);
			}
			result.Reverse();
			return result;
		}*/

		public bool IsInsideMap() {
			return X >= 0 && X < Config.MAP_WIDTH && Y >= 0 && Y < Config.MAP_HEIGHT;
		}

		public int DistanceTo(Coordinate coordinate) {
			var key = $"{this}|{coordinate}";

			int result;
			if (DISTANCE_CACHE.TryGetValue(key, out result)) {
				return result;
			}
			result = ToCubeCoordinate().DistanceTo(coordinate);
			DISTANCE_CACHE.Add(key, result);
			return result;
		}

		public IEnumerable<Coordinate> Ring(int range) {
			var coordinate = this;
			for (var i = 0; i < range; i++) {
				coordinate = coordinate.Neighbor(Orientation.DownLeft);
			}

			for (var i = 0; i < 6; i++) {
				for (var j = 0; j < range; j++) {
					if (coordinate.IsInsideMap()) {
						yield return coordinate;
					}
					coordinate = coordinate.Neighbor((Orientation)i);
				}
			}
		} 

		public CubeCoordinate ToCubeCoordinate() {
			var xp = X - (Y - (Y & 1))/2;
			var zp = Y;
			var yp = -(xp + zp);
			return new CubeCoordinate(xp, yp, zp);
		}

		public override bool Equals(object obj) {
			var item = obj as Coordinate;
			return item != null && Equals(item);
		}

		public bool Equals(Coordinate other) {
			return X == other.X && Y == other.Y;
		}

		public override int GetHashCode() {
			unchecked {
				return (X.GetHashCode()*397) ^ Y.GetHashCode();
			}
		}

		public override string ToString() {
			return $"{X} {Y}";
		}
	}
}
