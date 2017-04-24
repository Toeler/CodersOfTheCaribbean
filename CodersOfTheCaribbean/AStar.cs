using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodersOfTheCaribbean {
	public class GraphNode {
		public Coordinate Coordinate { get; }
		public Orientation Orientation { get; }
		public int Speed { get; }

		public GraphNode(Coordinate coordinate, Orientation orientation, int speed) {
			Coordinate = coordinate;
			Orientation = orientation;
			Speed = speed;
		}

		public IEnumerable<GraphNode> Neighbors() {
			var speed = Speed;
			var posAtEndofTurn = Coordinate;
			for (var i = 0; i < Speed; i++) {
				var newPos = posAtEndofTurn.Neighbor(Orientation);
				if (newPos.IsInsideMap()) {
					posAtEndofTurn = newPos;
				} else {
					speed = 0;
					break;
				}
			}

			yield return new GraphNode(posAtEndofTurn, Orientation, speed);
			yield return new GraphNode(posAtEndofTurn, Orientation.Next(), speed);
			yield return new GraphNode(posAtEndofTurn, Orientation.Prev(), speed);

			if (speed < Config.MAX_SHIP_SPEED) {
				var newPos = posAtEndofTurn.Neighbor(Orientation);
				if (newPos.IsInsideMap()) {
					posAtEndofTurn = newPos;
				} else {
					speed = 0;
				}
				yield return new GraphNode(posAtEndofTurn, Orientation, speed + 1);
			}
			if (speed > 0) {
				posAtEndofTurn = posAtEndofTurn.Neighbor(Orientation.Opposite());
				yield return new GraphNode(posAtEndofTurn, Orientation, speed - 1);
			}
		}

		public override bool Equals(object obj) {
			var item = obj as GraphNode;
			return item != null && Equals(item);
		}

		public bool Equals(GraphNode other) {
			return Orientation == other.Orientation && Speed == other.Speed && Coordinate.Equals(other.Coordinate);
		}

		public override int GetHashCode() {
			unchecked {
				var hashCode = Coordinate?.GetHashCode() ?? 0;
				hashCode = (hashCode*397) ^ (int)Orientation;
				hashCode = (hashCode*397) ^ Speed;
				return hashCode;
			}
		}

		public override string ToString() {
			return $"({Coordinate} F: {Orientation} S: {Speed})";
		}
	}

	public class AStar {
		private static GraphNode ExtractNode(IDictionary<GraphNode, double> costSoFar, Coordinate target) {
			return costSoFar.Where(pair => pair.Key.Coordinate.Equals(target)).OrderBy(pair => pair.Value).First().Key;
		}

		private static IList<GraphNode> ExtractPath(IDictionary<GraphNode, GraphNode> cameFrom, IDictionary<GraphNode, double> costSoFar, GraphNode target) {
			var result = new List<GraphNode>();
			var pathItem = target;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			while(pathItem != null && costSoFar[pathItem] != 0) {
				result.Add(pathItem);
				cameFrom.TryGetValue(pathItem, out pathItem);
			}
			result.Reverse();
			return result;
		}

		public static IList<GraphNode> GetPath(GameState gameState, GraphNode startNode, Coordinate target, bool bowCounts = false) {
			Console.Error.WriteLine($"Begin pathing for {startNode} to {target}");
			// TODO: Consider colliding with other ships
			// TODO: Consider bow hitting the target
			// TODO: Consider bow/stern hitting a mine when reaching target
			IDictionary<GraphNode, GraphNode> cameFrom = new Dictionary<GraphNode, GraphNode>();
			IDictionary<GraphNode, double> costSoFar = new Dictionary<GraphNode, double>();
			using (new DisposableStopwatch(t => Console.Error.WriteLine($"Pathing complete for {startNode} to {target}. Took {t.TotalMilliseconds}ms to calculate. {ExtractPath(cameFrom, costSoFar, ExtractNode(costSoFar, target)).Count} steps, cost {costSoFar[ExtractNode(costSoFar, target)]}, visited {costSoFar.Keys.Count} nodes."))) {
				var frontier = new PriorityQueue<PriorityQueueItem<GraphNode>>();
				frontier.Enqueue(new PriorityQueueItem<GraphNode>(startNode, 0));

				cameFrom[startNode] = startNode;
				costSoFar[startNode] = 0;

				var count = 0;
				var found = false;
				while (frontier.Count() > 0) {
					++count;
					var current = frontier.Dequeue();

					if (current.Item.Coordinate.Equals(target) || (bowCounts && current.Item.Coordinate.Neighbor(current.Item.Orientation).Equals(target))) {
						cameFrom[new GraphNode(target, current.Item.Orientation, 0)] = current.Item;
						costSoFar[new GraphNode(target, current.Item.Orientation, 0)] = costSoFar[current.Item];
						break;
					}

					if(current.Priority > 3 && found) {
						//Console.Error.WriteLine($"{count}: {current.Item} {costSoFar[current.Item]}");
					}

					foreach (var next in current.Item.Neighbors()) {
						var newCost = costSoFar[current.Item] + 1;
						/*if (target.X == 20 && target.Y == 13 && ++count >= 500) {
							Console.Error.WriteLine($"{count}: {next} {newCost}");
						}*/
						var preRotateBow = next.Coordinate.Neighbor(current.Item.Orientation);
						var nextBow = next.Coordinate.Neighbor(next.Orientation);
						var nextStern = next.Coordinate.Neighbor(next.Orientation.Opposite());
						var currentPathLength = ExtractPath(cameFrom, costSoFar, current.Item).Count + 1;

						var skip = false;
						foreach (var ship in gameState.Ships.Where(ship => !ship.Position.Equals(startNode.Coordinate))) {
							if (ship.Position.Equals(next.Coordinate) || ship.Bow.Equals(next.Coordinate) || ship.Stern.Equals(next.Coordinate)) {
								if (currentPathLength == 1) {
									skip = true; // Ship is in way
									break;
								}
								//newCost += 3; // Unincentivize but don't rule out as we aren't predicting movement
							}
						}
						if (skip) {
							continue;
						}

						if (
							gameState.Mines.FirstOrDefault(
								mine =>
									mine.Position.Equals(next.Coordinate) || mine.Position.Equals(preRotateBow) || mine.Position.Equals(nextBow) || mine.Position.Equals(nextStern)) !=
							null) {
							newCost += Config.MINE_DAMAGE;
						}

						foreach (var cannonball in gameState.Cannonballs.Where(cannonball => cannonball.RemainingTurns == currentPathLength)) {
							if (cannonball.Position.Equals(next.Coordinate)) {
								newCost += Config.HIGH_DAMAGE;
							} else if (cannonball.Position.Equals(preRotateBow) || cannonball.Position.Equals(nextBow) || cannonball.Position.Equals(nextStern)) {
								newCost += Config.LOW_DAMAGE;
							}
						}

						if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next]) {
							var priority = newCost + next.Coordinate.DistanceTo(target);
							if (next.Coordinate.Equals(target)) {
								found = true;
								//Console.Error.WriteLine("FOUND AT " + newCost + " COST SET PRIORITY TO " + priority);
								//Console.Error.WriteLine($"Distance from {next.Coordinate} to {target} = {next.Coordinate.DistanceTo(target)}");
							}
							costSoFar[next] = newCost;
							frontier.Enqueue(new PriorityQueueItem<GraphNode>(next, priority));
							cameFrom[next] = current.Item;
						}
					}
				}
			}

			if (costSoFar.Keys.Count > 1500) {
				Console.Error.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("\r\n", costSoFar.Keys.Skip(1200).Take(100)))));
			}

			return ExtractPath(cameFrom, costSoFar, ExtractNode(costSoFar, target));
		}
	}
}
