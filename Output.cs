using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections;

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
namespace CodersOfTheCaribbean {
	public class Cannonball : Entity {
		public override EntityType Type => EntityType.Cannonball;

		public string Owner { get; }
		public int RemainingTurns { get; set; }
		private int _remainingTurns;

		public Cannonball(string[] data) : base(data) {
			Owner = data[4];
		}

		public override void Update(string[] data) {
			base.Update(data);
			RemainingTurns = int.Parse(data[5]);
		}

		public override void Save() {
			base.Save();
			_remainingTurns = RemainingTurns;
		}

		public override void Reset() {
			base.Reset();
			RemainingTurns = _remainingTurns;
		}

		public override string ToString() {
			return $"{base.ToString()} TURNS: {RemainingTurns}";
		}

		public static Cannonball CreateDummy(Coordinate target, string ownerId, int travelTime) {
			return new Cannonball(new[] { "-1", EntityType.Cannonball.ToString(), target.X.ToString(), target.Y.ToString(), ownerId, travelTime.ToString() });
		}
	}
}
namespace CodersOfTheCaribbean {
	public static class Config {
		public const int SEED = 42;

		public const int TURN_1_LIMIT = 1000;
		public const int TURN_LIMIT = 50;

		public const string OWNER_ID = "1";

		public const int MAP_WIDTH = 23;
		public const int MAP_HEIGHT = 21;

		public const int MAX_SHIP_HEALTH = 100;
		public const int MAX_SHIP_SPEED = 2;

		public const bool CANNONS_ENABLED = true;
		public const int COOLDOWN_CANNON = 2;
		public const int FIRE_DISTANCE_MAX = 10;
		public const int HIGH_DAMAGE = 50;
		public const int LOW_DAMAGE = 25;

		public const bool MINES_ENABLED = true;
		public const int COOLDOWN_MINE = 5;
		public const int MINE_DAMAGE = 25;
		public const int NEAR_MINE_DAMAGE = 10;

		public const int REWARD_RUM_BARREL_VALUE = 30;

		public const int SIM_DEPTH = 5;
		public const int POOL_SIZE = 50;
		public const int MUTATION = 2;
	}
}

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

namespace CodersOfTheCaribbean {
	public class CubeCoordinate {
		private static readonly int[][] DIRECTIONS = {
			new[] {  1, -1,  0 },
			new[] {  1,  0, -1 },
			new[] {  0,  1, -1 },
			new[] { -1,  1,  0 },
			new[] { -1,  0,  1 },
			new[] {  0, -1,  1 }
		};

		private int X { get; }
		private int Y { get; }
		private int Z { get; }

		public CubeCoordinate(int x, int y, int z) {
			X = x;
			Y = y;
			Z = z;
		}

		public Coordinate ToOffsetCoordinate() {
			var newX = (X + (Z - (Z & 1))/2);
			var newY = Z;
			return new Coordinate(newX, newY);
		}

		public CubeCoordinate Neighbor(Orientation orientation) {
			var directions = DIRECTIONS[(int)orientation];
			var nx = (X + directions[0]);
			var ny = (Y + directions[1]);
			var nz = (Z + directions[2]);
			return new CubeCoordinate(nx, ny, nz);
		}

		public int DistanceTo(CubeCoordinate coordinate) {
			return (Math.Abs(X - coordinate.X) + Math.Abs(Y - coordinate.Y) + Math.Abs(Z - coordinate.Z))/2;
		}

		public int DistanceTo(Coordinate coordinate) {
			return DistanceTo(coordinate.ToCubeCoordinate());
		}

		public override string ToString() {
			return $"{X} {Y} {Z}";
		}
	}
}
namespace CodersOfTheCaribbean {
	public class Damage {
		public Coordinate Position { get; }
		public int Health { get; }
		public bool Hit { get; }

		public Damage(Coordinate position, int health, bool hit) {
			Position = position;
			Health = health;
			Hit = hit;
		}
	}
}

namespace CodersOfTheCaribbean {
	public class DisposableStopwatch : IDisposable {
		private readonly Stopwatch _sw;
		private readonly Action<double> _fTick;
		private readonly Action<TimeSpan> _fMs;

		public DisposableStopwatch(Action<double> f) {
			_fTick = f;
			_sw = Stopwatch.StartNew();
		}

		public DisposableStopwatch(Action<TimeSpan> f) {
			_fMs = f;
			_sw = Stopwatch.StartNew();
		}

		public void Dispose() {
			_sw.Stop();
			if (_fTick != null) {
				//_fTick(((double)_sw.ElapsedTicks / Stopwatch.Frequency) * 1000000000.0);
				_fTick(_sw.ElapsedTicks);
			}
			if(_fMs != null) {
				_fMs(_sw.Elapsed);
			}
		}
	}
}
namespace CodersOfTheCaribbean {
	public enum EntityType {
		Ship,
		RumBarrel,
		Mine,
		Cannonball
	};

	public abstract class Entity {
		public abstract EntityType Type { get; }
		public string Id { get; }
		public Coordinate Position { get; set; }
		private Coordinate _position;

		public Entity(string[] data) {
			Id = data[0];
			Update(data);
		}

		public virtual void Update(string[] data) {
			Position = new Coordinate(int.Parse(data[2]), int.Parse(data[3]));
		}

		public virtual void Save() {
			_position = Position;
		}

		public virtual void Reset() {
			Position = _position;
		}

		public override string ToString() {
			return $"{Type}: ID {Id} POS: {Position}";
		}
	}
}

namespace CodersOfTheCaribbean {
	public static class Extensions {
		/*public static T Next<T>(this T src) where T : struct {
			if(!typeof(T).IsEnum) {
				throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");
			}

			var arr = (T[])Enum.GetValues(src.GetType());
			var j = Array.IndexOf(arr, src) + 1;
			return arr.Length == j ? arr[0] : arr[j];
		}

		public static T Prev<T>(this T src) where T : struct {
			if(!typeof(T).IsEnum) {
				throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");
			}

			var arr = (T[])Enum.GetValues(src.GetType());
			var j = Array.IndexOf(arr, src) - 1;
			return j == -1 ? arr[arr.Length - 1] : arr[j];
		}*/

		public static Orientation Next(this Orientation src) {
			return (Orientation)(((int)src + 1)%6);
		}

		public static Orientation Prev(this Orientation src) {
			return (Orientation)(((int)src + 5) % 6);
		}

		public static Orientation Opposite(this Orientation src) {
			return (Orientation)(((int)src + 3)%6);
			var arr = (Orientation[])Enum.GetValues(typeof(Orientation));
			var j = Array.IndexOf(arr, src) + 3;
			return arr[j%arr.Length];
		}

		private static readonly IDictionary<EntityType, string> ENTITY_TYPE_TO_STRING = new Dictionary<EntityType, string> {
			{ EntityType.Cannonball, "CANNONBALL" },
			{ EntityType.Mine, "MINE" },
			{ EntityType.RumBarrel, "BARREL" },
			{ EntityType.Ship, "SHIP" }
		};
		public static string ToString(this EntityType src) {
			string value;
			ENTITY_TYPE_TO_STRING.TryGetValue(src, out value);
			return value;
		}
		private static readonly IDictionary<string, EntityType> STRING_TO_ENTITY_TYPE = new Dictionary<string, EntityType> {
			{ "CANNONBALL", EntityType.Cannonball },
			{ "MINE", EntityType.Mine },
			{ "BARREL", EntityType.RumBarrel },
			{ "SHIP", EntityType.Ship }
		};
		public static EntityType FromString(this Type t, string str) {
			EntityType value;
			STRING_TO_ENTITY_TYPE.TryGetValue(str, out value);
			return value;
		}

		public static ShipActionType PickRandom(this Type t) {
			var values = Enum.GetValues(t);
			return (ShipActionType)values.GetValue(Program.RANDOM.Next(values.Length));
		}

		public static double Increment(this ConcurrentDictionary<string, double> d, string key, double value) {
			return d.AddOrUpdate(key, value, (id, val) => val + value);
		}

		public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences) {
			IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
			return sequences.Aggregate(
			  emptyProduct,
			  (accumulator, sequence) =>
				from accseq in accumulator
				from item in sequence
				select accseq.Concat(new[] { item }));
		}

		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while(n > 1) {
				n--;
				int k = Program.RANDOM.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}

namespace CodersOfTheCaribbean {
	public class GameState {
		public List<RumBarrel> Barrels { get; set; }
		private IList<RumBarrel> _barrels;
		public List<Ship> Ships { get; private set; }
		private IList<Ship> _ships;
		public IEnumerable<Ship> AliveShips => Ships.Where(ship => ship.Health > 0);
		public IEnumerable<Ship> MyShips => Ships.Where(ship => ship.Owner == Config.OWNER_ID);
		public IEnumerable<Ship> MyAliveShips => MyShips.Where(ship => ship.Health > 0);
		public IEnumerable<Ship> EnemyShips => Ships.Where(ship => ship.Owner != Config.OWNER_ID);
		public List<Mine> Mines { get; private set; }
		private IList<Mine> _mines;
		public List<Cannonball> Cannonballs { get; private set; }
		private IList<Cannonball> _cannonballs;
		public IDictionary<string, Entity> Entities { get; private set; }

		public GameState() {
			_barrels = new List<RumBarrel>();
			_ships = new List<Ship>();
			_mines = new List<Mine>();
			_cannonballs = new List<Cannonball>();
			Entities = new Dictionary<string, Entity>();
		}

		public void Save() {
			_barrels = new List<RumBarrel>(Barrels);
			_ships = new List<Ship>(Ships);
			_mines = new List<Mine>(Mines);
			_cannonballs = new List<Cannonball>(Cannonballs);
		}

		public void Reset() {
			Barrels = new List<RumBarrel>(_barrels);
			Ships = new List<Ship>(_ships);
			Mines = new List<Mine>(_mines);
			Cannonballs = new List<Cannonball>(_cannonballs);

			foreach (var entity in Entities.Values) {
				entity.Reset();
			}
		}

		public void ParseInputs(ref Stopwatch turnTimer) {
			Barrels = new List<RumBarrel>();
			Ships = new List<Ship>();
			Mines = new List<Mine>();
			Cannonballs = new List<Cannonball>();

			var oldEntities = Entities;
			Entities = new Dictionary<string, Entity>();

			using (new DisposableStopwatch(t => Console.Error.WriteLine($"First read turn took {t.TotalMilliseconds}ms"))) {
				var myShipCount = int.Parse(Console.ReadLine()); // the number of remaining ships
				turnTimer.Start();
			}
			var entityCount = int.Parse(Console.ReadLine()); // the number of entities (e.g. ships, mines or cannonballs)

			for (var i = 0; i < entityCount; i++) {
				Entity entity;
				var entityData = Console.ReadLine().Split(' ');
				var entityId = entityData[0];
				var entityType = typeof(EntityType).FromString(entityData[1]);
				
				if (oldEntities.TryGetValue(entityId, out entity)) {
					entity.Update(entityData);
				}

				switch (entityType) {
					case EntityType.Ship: {
						if (entity == null) {
							entity = new Ship(entityData);
						}
						Ships.Add(entity as Ship);
						break;
					}
					case EntityType.RumBarrel: {
						if (entity == null) {
							entity = new RumBarrel(entityData);
						}
						Barrels.Add(entity as RumBarrel);
						break;
					}
					case EntityType.Mine: {
						if (entity == null) {
							entity = new Mine(entityData);
						}
						Mines.Add(entity as Mine);
						break;
					}
					case EntityType.Cannonball: {
						if (entity == null) {
							entity = new Cannonball(entityData);
						}
						Cannonballs.Add(entity as Cannonball);
						break;
					}
					default: {
						throw new ArgumentOutOfRangeException($"Unknown EntityType {entityType}");
					}
				}
				Entities.Add(entityId, entity);
				entity.Save();
			}
		}
	}
}

namespace CodersOfTheCaribbean {
	public class Mine : Entity {
		public override EntityType Type => EntityType.Mine;

		public Mine(string[] data) : base(data) {
		}

		public IEnumerable<Damage> Explode(IEnumerable<Ship> ships, bool force) {
			IList<Damage> damage = new List<Damage>();
			Ship victim = null;

			foreach (var ship in ships.Where(ship => Position.Equals(ship.Position) || Position.Equals(ship.Bow) || Position.Equals(ship.Stern))) {
				damage.Add(new Damage(Position, Config.MINE_DAMAGE, true));
				ship.Damage(Config.MINE_DAMAGE);
				victim = ship;
			}

			if (force || victim != null) {
				if (victim == null) {
					damage.Add(new Damage(Position, Config.MINE_DAMAGE, true));
				}

				foreach (var ship in ships.Where(ship => ship != victim)) {
					Coordinate impactPosition = null;
					if (ship.Stern.DistanceTo(Position) <= 1) {
						impactPosition = ship.Stern;
					} else if (ship.Bow.DistanceTo(Position) <= 1) {
						impactPosition = ship.Bow;
					} else if (ship.Position.DistanceTo(Position) <= 1) {
						impactPosition = ship.Position;
					}

					if (impactPosition != null) {
						ship.Damage(Config.NEAR_MINE_DAMAGE);
						damage.Add(new Damage(impactPosition, Config.NEAR_MINE_DAMAGE, true));
					}
				}
			}

			return damage;
		}

		public static Mine CreateDummy(Coordinate target) {
			return new Mine(new[] { "-1", EntityType.Mine.ToString(), target.X.ToString(), target.Y.ToString() });
		}
	}
}

namespace CodersOfTheCaribbean {
	public class PriorityQueueItem<T> : IComparable<PriorityQueueItem<T>> {
		public T Item { get; set; }
		public double Priority { get; set; }

		public PriorityQueueItem(T item, double priority) {
			Item = item;
			Priority = priority;
		}

		public int CompareTo(PriorityQueueItem<T> other) {
			return Priority.CompareTo(other.Priority);
		}
	}

	/// <summary>
	/// A simplified priority queue implementation.  Is stable, auto-resizes, and thread-safe, at the cost of being slightly slower than
	/// FastPriorityQueue
	/// </summary>
	/// <typeparam name="TItem">The type to enqueue</typeparam>
	/// <typeparam name="TPriority">The priority-type to use for nodes.  Must extend IComparable&lt;TPriority&gt;</typeparam>
	public class PriorityQueue<T> where T : IComparable<T> {
		private List<T> data;

		public PriorityQueue() {
			this.data = new List<T>();
		}

		public void Enqueue(T item) {
			data.Add(item);
			int ci = data.Count - 1; // child index; start at end
			while(ci > 0) {
				int pi = (ci - 1) / 2; // parent index
				if(data[ci].CompareTo(data[pi]) >= 0) break; // child item is larger than (or equal) parent so we're done
				T tmp = data[ci]; data[ci] = data[pi]; data[pi] = tmp;
				ci = pi;
			}
		}

		public T Dequeue() {
			// assumes pq is not empty; up to calling code
			int li = data.Count - 1; // last index (before removal)
			T frontItem = data[0];   // fetch the front
			data[0] = data[li];
			data.RemoveAt(li);

			--li; // last index (after removal)
			int pi = 0; // parent index. start at front of pq
			while(true) {
				int ci = pi * 2 + 1; // left child index of parent
				if(ci > li) break;  // no children so done
				int rc = ci + 1;     // right child
				if(rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
					ci = rc;
				if(data[pi].CompareTo(data[ci]) <= 0) break; // parent is smaller than (or equal to) smallest child so done
				T tmp = data[pi]; data[pi] = data[ci]; data[ci] = tmp; // swap parent and child
				pi = ci;
			}
			return frontItem;
		}

		public T Peek() {
			T frontItem = data[0];
			return frontItem;
		}

		public int Count() {
			return data.Count;
		}

		public override string ToString() {
			string s = "";
			for(int i = 0; i < data.Count; ++i)
				s += data[i].ToString() + " ";
			s += "count = " + data.Count;
			return s;
		}

		public bool IsConsistent() {
			// is the heap property true for all data?
			if(data.Count == 0) return true;
			int li = data.Count - 1; // last index
			for(int pi = 0; pi < data.Count; ++pi) // each parent index
			{
				int lci = 2 * pi + 1; // left child index
				int rci = 2 * pi + 2; // right child index

				if(lci <= li && data[pi].CompareTo(data[lci]) > 0) return false; // if lc exists and it's greater than parent then bad.
				if(rci <= li && data[pi].CompareTo(data[rci]) > 0) return false; // check the right child too.
			}
			return true;
		}
	}
}

namespace CodersOfTheCaribbean {
	public static class Program {
		public static readonly Random RANDOM = new Random(Config.SEED);

		public static readonly ConcurrentDictionary<string, double> times = new ConcurrentDictionary<string, double>();/* {
			{ "MoveCannonballs", 0 },
			{ "ApplyActions", 0 },
			{ "MoveShips", 0 },
			{ "RotateShips", 0 },
			{ "ExplodeShips", 0 },
			{ "ExplodeMines", 0 },
			{ "ExplodeBarrels", 0 },
			{ "CheckBarrelCollisions", 0 },
			{ "CheckMineCollisions", 0 },
			{ "RotateShipsNewBow", 0 },
			{ "RotateShipsNewStern", 0 },
			{ "RotateShipsCheckCollisions", 0 },
			{ "MoveShipsCheckCollisions", 0 },
			{ "CheckCollisions", 0 },
			{ "CheckCollisionsBow", 0 },
			{ "CheckCollisionsStern", 0 }
		};*/
		public static Stopwatch tempStopwatch;
		public static int tempTurn;

		static Coordinate PositionInFuture(Ship ship, int turns) {
			var futurePosition = ship.Position;
			for (var i = 0; i < turns; i++) {
				for (var j = 0; j < ship.Speed; j++) {
					futurePosition = futurePosition.Neighbor(ship.Orientation);
				}
			}
			return futurePosition;
		}

		static Coordinate CalculateFutureShotPosition(GameState gameState, Ship myShip, Ship enemyShip) {
			// TODO: Shoot at mines or barrels that the enemy is heading to
			Coordinate result = null;
			var turnsOfMovement = 0;
			var distance = -1;
			var turnsToHit = -1;
			var myBow = myShip.Bow;
			for (var i = 0; i < myShip.Speed; i++) {
				myBow = myBow.Neighbor(myShip.Orientation);
			}

			while (turnsToHit != turnsOfMovement && turnsOfMovement < 7) {
				result = PositionInFuture(enemyShip, ++turnsOfMovement);
				distance = myBow.DistanceTo(result);
				turnsToHit = (int)(1 + Math.Round(distance / 3.0));
				//Console.Error.WriteLine($"In {turnsOfMovement} turns enemy will be at {result} and I could hit it in {turnsToHit}");
			}
			if (turnsToHit != turnsOfMovement) {
				result = null;
			}
			if (result != null && result.IsInsideMap() && distance < 10) {
				//Console.Error.WriteLine($"My bow is at {myBow} ({myShip.Position} f: {myShip.Orientation} s: {myShip.Speed}), it will take {turnsToHit} turns to hit {result} at a distance of {distance}");
			}
			return result != null && result.IsInsideMap() && distance < 10 ? result : null;
		}

		private static bool IsValidTurn(IList<ShipActionType> actions) {
			var mineIndex = -1;
			var fireIndex = -1;
			for (var i = 0; i < actions.Count; i++) {
				if (actions[i] == ShipActionType.FIRE) {
					if (fireIndex > -1) {
						if (i - fireIndex < Config.COOLDOWN_CANNON) {
							return false;
						}
					}
					fireIndex = i;
				}
				if (actions[i] == ShipActionType.MINE) {
					if (mineIndex > -1) {
						if (i - mineIndex < Config.COOLDOWN_MINE) {
							return false;
						}
					}
					mineIndex = i;
				}
			}

			return true;
		}

		public static void Main3(string[] args) {
			var gameState = new GameState();
			var turn = 0;
			IList<IEnumerable<ShipActionType>> allPossibleActions = new List<IEnumerable<ShipActionType>>();

			IDictionary<string, IList<ShipAction>> bestShipActions = new Dictionary<string, IList<ShipAction>>();
			var bestScore = double.MinValue;
			while (true) {
				var turnStopwatch = new Stopwatch();
				//turnStopwatch.Start();

				using(new DisposableStopwatch(t => Console.Error.WriteLine($"Took {t.TotalMilliseconds}ms to parse inputs"))) {
					gameState.ParseInputs(ref turnStopwatch);
				}
				gameState.Save();

				if (turn == 0) {
					using (new DisposableStopwatch(t => Console.Error.WriteLine($"Took {t.TotalMilliseconds}ms to generate turns"))) {
						var values = Enum.GetValues(typeof(ShipActionType)).Cast<ShipActionType>().Where(x => x != ShipActionType.WAIT).ToList();
						allPossibleActions =
							Enumerable.Range(0, Config.SIM_DEPTH)
									  .Select(_ => values)
									  .CartesianProduct()
									  .Where(x => IsValidTurn(x.ToList()))
									  .ToList();

						//Console.Error.WriteLine(string.Join("\r\n", actions.Select(x => string.Join(" -> ", x))));
						//Console.Error.WriteLine(actions.Count());
					}
				} else {
					foreach (var actions in bestShipActions.Values) {
						actions.RemoveAt(0);
						if (actions.Count == 0) {
							bestScore = double.MinValue;
							break;
						}
					}
				}

				IDictionary<string, IList<IEnumerable<ShipActionType>>> shipActions = new Dictionary<string, IList<IEnumerable<ShipActionType>>>();
				foreach (var ship in gameState.MyShips) {
					var thisShipsActions = new List<IEnumerable<ShipActionType>>(allPossibleActions);
					thisShipsActions.Shuffle();

					shipActions.Add(ship.Id, thisShipsActions);
				}

				var timeLimit = turn > 0 ? 47 : 950;
				var index = 0;
				IDictionary<string, Coordinate> optimalTargets = new Dictionary<string, Coordinate>();
				foreach (var ship in gameState.MyShips) {
					optimalTargets.Add(ship.Id, CalculateFutureShotPosition(gameState, ship,
						gameState.EnemyShips.OrderBy(enemy => ship.Bow.DistanceTo(enemy.Position)).First()));
				}
				while (turnStopwatch.ElapsedMilliseconds < timeLimit && index < shipActions.Values.First().Count - 1) {
					IDictionary<string, IList<ShipAction>> testShipActions = new Dictionary<string, IList<ShipAction>>();
					foreach (var ship in gameState.MyShips) {
						var actionToTest = shipActions[ship.Id][index];
						testShipActions.Add(ship.Id, actionToTest.Select(type => {
							Coordinate target = null;
							if (type == ShipActionType.FIRE) {
								target = optimalTargets[ship.Id];
							}
							return new ShipAction(type, target);
						}).Where(x => x.Type != ShipActionType.FIRE || x.Target != null).ToList());
					}

					var score = Simulator.Run(gameState, testShipActions);

					if (score > bestScore) {
						bestShipActions = testShipActions;
						bestScore = score;
					}

					index++;
				}
				Console.Error.WriteLine($"Best Actions (Score {bestScore}):\r\n{string.Join("\r\n" , bestShipActions.Select(pair => $"{pair.Key}: {string.Join(" -> ", pair.Value)}"))}");

				foreach(var ship in gameState.MyShips) {
					ship.OutputAction(bestShipActions[ship.Id][0]);
				}

				Console.Error.WriteLine($"Turn {turn++} took {turnStopwatch.ElapsedMilliseconds}ms");
			}
		}

		private static ShipActionType ActionRequired(GraphNode node1, GraphNode node2) {
			if (node1.Orientation == node2.Orientation) {
				if (node1.Speed == node2.Speed) {
					return ShipActionType.WAIT;
				}
				return node1.Speed < node2.Speed ? ShipActionType.FASTER : ShipActionType.SLOWER;
			}

			return node1.Orientation.Next() == node2.Orientation ? ShipActionType.PORT : ShipActionType.STARBOARD;
		}

		static void Main(string[] args) {
			// TODO: Sacrifice own ship to increase health above enemy

			var gameState = new GameState();
			var turn = 0;
			
			using(new DisposableStopwatch(t => Console.Error.WriteLine($"Took {t.TotalMilliseconds}ms to populate distance cache"))) {
				for (var x = 0; x < Config.MAP_WIDTH; x++) {
					for (var y = 0; y < Config.MAP_HEIGHT; y++) {
						var point1 = new Coordinate(x, y);

						for (var x2 = 0; x2 < Config.MAP_WIDTH; x2++) {
							for (var y2 = 0; y2 < Config.MAP_HEIGHT; y2++) {
								point1.DistanceTo(new Coordinate(x2, y2));
							}
						}
					}
				}
			}

			while (true) {
				var turnStopwatch = new Stopwatch();
				//turnStopwatch.Start();

				using(new DisposableStopwatch(t => Console.Error.WriteLine($"Took {t.TotalMilliseconds}ms to parse inputs"))) {
					gameState.ParseInputs(ref turnStopwatch);
				}
				gameState.Save();

				var targetsToConsider = Math.Max((int)(((turn == 0 ? Config.TURN_1_LIMIT : Config.TURN_LIMIT) - turnStopwatch.ElapsedMilliseconds)/6 /*ms per path*/)/gameState.MyShips.Count(), 1);
				var currentWinner = gameState.Ships.OrderBy(s => s.Health).Last();
				var pickedBarrels = new List<RumBarrel>();
				var alreadySuiciding = false;

				IDictionary<RumBarrel, IOrderedEnumerable<Tuple<Ship, int>>> targetsByBarrel;
				IDictionary<Ship, IOrderedEnumerable<Tuple<RumBarrel, IList<GraphNode>>>> targetsByShip;
				IDictionary<Ship, Tuple<RumBarrel, IList<GraphNode>>> bestMovesByShip = new Dictionary<Ship, Tuple<RumBarrel, IList<GraphNode>>>();
				using (new DisposableStopwatch(t => Console.Error.WriteLine($"Picking targets took {t.TotalMilliseconds}ms"))) {
					targetsByShip = gameState.MyShips.ToDictionary(ship => ship,
						ship =>
							gameState.Barrels.OrderBy(barrel => barrel.Position.DistanceTo(ship.Position))
									 .Take(targetsToConsider)
									 .Select(
										 barrel =>
											 new Tuple<RumBarrel, IList<GraphNode>>(barrel,
												 AStar.GetPath(gameState, new GraphNode(ship.Position, ship.Orientation, ship.Speed), barrel.Position, true)))
									 .OrderBy(arg => arg.Item2.Count)
									 .ThenByDescending(arg => arg.Item1.Health));
					/*targetsByBarrel = gameState.Barrels.ToDictionary(barrel => barrel, barrel => {
						return
							targetsByShip.SelectMany(
								pair => pair.Value.Where(x => barrel == x.Item1).Select(x => new Tuple<Ship, int>(pair.Key, x.Item2.Count)))
										 .OrderBy(x => x.Item2);
					});*/

					var shipsThatNeedTarget = gameState.MyShips.ToList();
					while (shipsThatNeedTarget.Count > 0) {
						var ship = shipsThatNeedTarget.First();
						var index = 0;
						var target = targetsByShip[ship].ElementAtOrDefault(index++);
						while (target != null) {
							var moves = target.Item2.Count;
							var existingTarget = bestMovesByShip.FirstOrDefault(pair => pair.Value.Item1.Equals(target.Item1));
							if (!existingTarget.Equals(default(KeyValuePair<Ship, Tuple<RumBarrel, IList<GraphNode>>>))) {
								var otherMoves = existingTarget.Value.Item2.Count;
								if (moves < otherMoves) {
									bestMovesByShip.Remove(existingTarget.Key);
									shipsThatNeedTarget.Add(existingTarget.Key);
									bestMovesByShip[ship] = target;
									break;
								}
								if (moves == otherMoves) {
									// TODO: Break a tie, for not just let first in first served
									target = targetsByShip[ship].ElementAtOrDefault(index++);
								} else {
									target = targetsByShip[ship].ElementAtOrDefault(index++);
								}
							} else {
								bestMovesByShip[ship] = target;
								break;
							}
						}
						shipsThatNeedTarget.Remove(ship);
					}
				}

				Console.Error.WriteLine(string.Join("\r\n", bestMovesByShip.Select(pair => $"{pair.Key.Id}: {string.Join(" -> ", pair.Value.Item2)}")));

				foreach(var ship in gameState.MyShips) {
					using (new DisposableStopwatch(t => Console.Error.WriteLine($"Ship {ship.Id} turn took {t.TotalMilliseconds}ms"))) {
						//var sortedBarrels = gameState.Barrels.Where(barrel => !pickedBarrels.Contains(barrel)).OrderBy(barrel => barrel.Position.DistanceTo(ship.Position));
						var sortedBarrels = new List<RumBarrel>();
						var nearbyEnemies =
							gameState.EnemyShips.Where(enemy => enemy.Position.DistanceTo(ship.Bow) < 15)
									 .OrderBy(enemy => enemy.Position.DistanceTo(ship.Bow));

						var startNode = new GraphNode(ship.Position, ship.Orientation, ship.Speed);
						Tuple<RumBarrel, IList<GraphNode>> bestMove;
						if (bestMovesByShip.TryGetValue(ship, out bestMove)) {
							var actionRequired = ActionRequired(startNode, bestMove.Item2.First());
							ship.Action = new ShipAction(actionRequired);
						} else if (sortedBarrels.Any()) {
							IList<GraphNode> bestMoves = null;
							RumBarrel closestBarrel = null;
							foreach (var barrel in sortedBarrels.Take(targetsToConsider)) {
								var optimalPath = AStar.GetPath(gameState, startNode, barrel.Position, true);
								// ReSharper disable once CompareOfFloatsByEqualityOperator
								// ReSharper disable once PossibleNullReferenceException
								if (bestMoves == null || optimalPath.Count < bestMoves.Count ||
									(optimalPath.Count == bestMoves.Count && barrel.Health > closestBarrel.Health)) {
									closestBarrel = barrel;
									bestMoves = optimalPath;
								}
							}
							Console.Error.WriteLine($"{startNode} -> {string.Join(" -> ", bestMoves)}");
							if (bestMoves.Any()) {
								var actionRequired = ActionRequired(startNode, bestMoves.First());
								/*var pos = bestMoves.First();
								if (pos.Speed == 2 || actionRequired == ShipActionType.FASTER) {
									var posIn2Turns = pos.Coordinate.Neighbor(pos.Orientation).Neighbor(pos.Orientation);
									if (gameState.Mines.Any(mine => mine.Position.Equals(posIn2Turns))) {
										if (pos.Speed == 2) {
											actionRequired = ShipActionType.SLOWER;
										} else {
											actionRequired = ShipActionType.WAIT;
										}
									}
								}*/
								ship.Action = new ShipAction(actionRequired);
								pickedBarrels.Add(closestBarrel);
							} else {
								// Pick new target and generate longer path
								ship.Action = new ShipAction(ShipActionType.WAIT);
							}
						} else {
							if (!alreadySuiciding && ship.Health < 40 && currentWinner.Owner != Config.OWNER_ID && gameState.MyShips.Count() > 1) {
								var target = gameState.MyShips.Where(s => s != ship).OrderBy(s => ship.Position.DistanceTo(s.Position)).First();
								if (ship.Position.DistanceTo(target.Position) < 6) {
									Console.Error.WriteLine($"{ship.Id}: GOODBYE SWEET WORLD");
									if (ship.Speed == 0) {
										ship.Fire(ship.Position);
									} else {
										ship.Fire(ship.Bow);
									}
									ship.CannonCooldown = Config.COOLDOWN_CANNON;
								} else {
									var targetPos = target.Bow.IsInsideMap() ? target.Bow : target.Position;
									var actionRequired = ActionRequired(startNode, AStar.GetPath(gameState, startNode, targetPos).First());
									ship.Action = new ShipAction(actionRequired);
								}
								alreadySuiciding = true;
							} else {
								var nearestEnemy = gameState.EnemyShips.OrderBy(enemy => enemy.Position.DistanceTo(ship.Position)).First();
								var moveToTarget = nearestEnemy.Position;
								var distance = moveToTarget.DistanceTo(ship.Position);

								if (distance < 7 || distance > 9 ||
									gameState.Cannonballs.Any(
										cannonball =>
											cannonball.Position.Equals(ship.Position) || cannonball.Position.Equals(ship.Bow) ||
											cannonball.Position.Equals(ship.Stern) ||
											gameState.Mines.Where(mine => mine.Position.Equals(cannonball.Position))
													 .Any(mine => mine.Position.DistanceTo(ship.Position) <= 2))) {
									var path = moveToTarget.Ring(8)
														   .Where(coordinate =>
															   coordinate.X > 1 && coordinate.X < Config.MAP_WIDTH - 1
															   && coordinate.Y > 1 && coordinate.Y < Config.MAP_HEIGHT - 1
															   && !gameState.Cannonballs.Any(cannonball =>
																   cannonball.Position.Equals(coordinate)
																   || cannonball.Position.Equals(coordinate.Neighbor(ship.Orientation))
																   || cannonball.Position.Equals(coordinate.Neighbor(ship.Orientation.Opposite()))
																   || cannonball.Position.DistanceTo(coordinate) < 2)
															   && !gameState.Mines.Any(mine =>
																   mine.Position.Equals(coordinate)
																   || mine.Position.Equals(coordinate.Neighbor(ship.Orientation))
																   || mine.Position.Equals(coordinate.Neighbor(ship.Orientation.Opposite()))
																   || mine.Position.DistanceTo(coordinate) < 2))
														   .OrderBy(coordinate => ship.Position.DistanceTo(coordinate))
														   .Take(targetsToConsider)
														   .Select(coordinate => AStar.GetPath(gameState, startNode, coordinate)).OrderBy(p => p.Count).First();
									Console.Error.WriteLine($"{startNode} -> {string.Join(" -> ", path)}");
									if (path.Any()) {
										var actionRequired = ActionRequired(startNode, path.First());
										var pos = path.First();
										Console.Error.WriteLine(path.First());
										if ((startNode.Speed == 2 && actionRequired == ShipActionType.WAIT) || (startNode.Speed == 1 && actionRequired == ShipActionType.FASTER)) {
											var posIn2Turns = pos.Coordinate.Neighbor(pos.Orientation).Neighbor(pos.Orientation);
											if (pos.Coordinate.X == 18 && pos.Coordinate.Y == 13) {
												Console.Error.WriteLine($"{pos} | {posIn2Turns}");
											}
											if(gameState.Mines.Any(mine => mine.Position.Equals(posIn2Turns) || mine.Position.Equals(posIn2Turns.Neighbor(pos.Orientation)))) {
												if (pos.Speed == 2) {
													actionRequired = ShipActionType.SLOWER;
												} else {
													actionRequired = ShipActionType.WAIT;
												}
											}
										}
										ship.Action = new ShipAction(actionRequired);
									} else {
										ship.Action = new ShipAction(ship.Speed > 0 ? ShipActionType.SLOWER : ShipActionType.WAIT);
									}
								} else {
									ship.Action = new ShipAction(ship.Speed > 0 ? ShipActionType.SLOWER : ShipActionType.WAIT);
								}
							}
						}

						if (ship.Action.Type == ShipActionType.WAIT && ship.CannonCooldown == 0 && nearbyEnemies.Any()) {
							var futureShotPosition = CalculateFutureShotPosition(gameState, ship, nearbyEnemies.First());
							if (futureShotPosition != null) {
								Console.Error.WriteLine($"{ship.Action}");
								ship.Fire(futureShotPosition);
								ship.CannonCooldown = Config.COOLDOWN_CANNON;
							}
						}


						if (ship.Action.Type == ShipActionType.WAIT && ship.MineCooldown == 0) {
							ship.Action = new ShipAction(ShipActionType.MINE);
						}

						ship.OutputAction();
					}
				}

				Console.Error.WriteLine($"Turn {turn++} took {turnStopwatch.ElapsedMilliseconds}ms");
			}
		}

		static void Main2(string[] args) {
			var gameState = new GameState();
			var turn = 0;
			var bestSolution = new Solution();

			while (true) {
				foreach (var key in times.Keys.ToList()) {
					times[key] = 0;
				}
				tempTurn = turn;
				var turnStopwatch = new Stopwatch();
				tempStopwatch = turnStopwatch;
				//turnStopwatch.Start();

				Simulator.Sims = 0;

				using (new DisposableStopwatch(t => Console.Error.WriteLine($"Took {t.TotalMilliseconds}ms to parse inputs"))) {
					gameState.ParseInputs(ref turnStopwatch);
				}
				gameState.Save();

				Solution baseSolution = null;
				if (turn > 0) {
					baseSolution = new Solution();

					foreach (var action in bestSolution.Actions) {
						var newActions = new ShipAction[Config.SIM_DEPTH];
						action.Value.Skip(1).ToArray().CopyTo(newActions, 0);
						baseSolution.Actions.Add(action.Key, newActions);
					}
				}

				var pool = new Solution[Config.POOL_SIZE];
				var newPool = new Solution[Config.POOL_SIZE];

				bestSolution = new Solution();
				var solution = new Solution();
				solution.Randomize(gameState);

				Simulator.Run(gameState, solution);
				pool[0] = solution;
				bestSolution.Copy(solution);

				var tempBest = solution;
//Console.Error.WriteLine("Mark start pool");
				var startI = 1;
				if (turn > 0) {
					for (var i = startI; i < Config.POOL_SIZE; i++) {
						var newSolution = new Solution();
						newSolution.Copy(baseSolution);

						foreach(var ship in gameState.MyAliveShips) {
							ShipAction[] actions;
							if(!newSolution.Actions.TryGetValue(ship.Id, out actions)) {
								actions = new ShipAction[Config.SIM_DEPTH];
								newSolution.Actions.Add(ship.Id, actions);
							}

							var actionType = typeof(ShipActionType).PickRandom();
							var target = new Coordinate(RANDOM.Next(Config.MAP_WIDTH), RANDOM.Next(Config.MAP_HEIGHT));
							actions[Config.SIM_DEPTH - 1] = new ShipAction(actionType, target);
						}
//Console.Error.WriteLine($"Part Pool {i} sim start");
						Simulator.Run(gameState, newSolution);
//Console.Error.WriteLine($"Part Pool {i} sim end");

						if (newSolution.Score > tempBest.Score) {
							tempBest = newSolution;
						}

						pool[i] = newSolution;
//Console.Error.WriteLine($"Pool {i} done");
					}

					startI = Config.POOL_SIZE/5;
				}
//Console.Error.WriteLine("Mark part pool");
				for (var i = startI; i < Config.POOL_SIZE; i++) {
					var newSolution = new Solution();
					newSolution.Randomize(gameState);
//Console.Error.WriteLine($"Pool {i} sim start");
					Simulator.Run(gameState, newSolution);
//Console.Error.WriteLine($"Pool {i} sim start");
					if (newSolution.Score > tempBest.Score) {
						tempBest = newSolution;
					}

					pool[i] = newSolution;
				}
//Console.Error.WriteLine($"tempBestScore: {tempBest.Score}, bestSolutionScore: {bestSolution.Score}");
				if (tempBest.Score > bestSolution.Score) {
					bestSolution.Copy(tempBest);
				}
				tempBest = bestSolution;

				var bestGeneration = 1;
				var generation = 1;
				var timeLimit = turn > 0 ? 47 : 950;
				
				while (turnStopwatch.ElapsedMilliseconds < timeLimit) {
					var newSolution = new Solution();
					newSolution.Copy(tempBest);
					newSolution.Mutate(gameState);
//if(Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"Main sim start");}
					Simulator.Run(gameState, newSolution);
//if(Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"Main sim end");}
					if (newSolution.Score > tempBest.Score) {
						tempBest = newSolution;
					}

					newPool[0] = newSolution;

					var poolFE = 1;
					while (poolFE < Config.POOL_SIZE && turnStopwatch.ElapsedMilliseconds < timeLimit) {
//Console.Error.WriteLine("123");
						var aIndex = RANDOM.Next(Config.POOL_SIZE);
						int bIndex;

						do {
							bIndex = RANDOM.Next(Config.POOL_SIZE);
						} while (bIndex == aIndex);
						
						var firstIndex = pool[aIndex].Score > pool[bIndex].Score ? aIndex : bIndex;

						do {
							aIndex = RANDOM.Next(Config.POOL_SIZE);
						} while(aIndex == firstIndex);

						do {
							bIndex = RANDOM.Next(Config.POOL_SIZE);
						} while(bIndex == aIndex || bIndex == firstIndex);

						var secondIndex = pool[aIndex].Score > pool[bIndex].Score ? aIndex : bIndex;

						var child = pool[firstIndex].Merge(pool[secondIndex]);
//Console.Error.WriteLine("456");
						if (RANDOM.Next(Config.MUTATION) != 0) {
							child.Mutate(gameState);
						}
//if(Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine("789 " + turnStopwatch.ElapsedMilliseconds + "ms");}
						Simulator.Run(gameState, child);
//if(Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine("!!!");}

						if (child.Score > tempBest.Score) {
							tempBest = child;
						}

						newPool[poolFE++] = child;
					}
//Console.Error.WriteLine("0");
					// Burn previous generation
					newPool.CopyTo(pool, 0);
					Array.Clear(newPool, 0, pool.Length);

					if (tempBest.Score > bestSolution.Score) {
						bestSolution.Copy(tempBest);
						bestGeneration = generation;
					}
					tempBest = bestSolution;

					generation++;
if (turnStopwatch.ElapsedMilliseconds > 40 && turnStopwatch.ElapsedMilliseconds < 50) {
					//Console.Error.WriteLine($"At {turnStopwatch.ElapsedMilliseconds}ms");
}
				}

				foreach (var ship in gameState.MyAliveShips) {
					ship.OutputAction(bestSolution);
				}

				Console.Error.WriteLine($"Turn {turn} took {turnStopwatch.ElapsedMilliseconds}ms");
				Console.Error.WriteLine($"Simulated {Simulator.Sims} times across {generation} generations, best was {bestGeneration}");
				Console.Error.WriteLine($"Best move: score: {bestSolution.Score} moves:\r\n{string.Join("\r\n", bestSolution.Actions.Select(pair => $"Ship: {pair.Key}\r\n{string.Join("\r\n", pair.Value.ToList())}"))}");
				//Console.Error.WriteLine($"Timers: \r\n{string.Join("\r\n", times.Select(pair => $"{pair.Key}: {Math.Round(pair.Value / 1000000.0)}ms"))}");
				//Console.Error.WriteLine($"Timers: \r\n{string.Join("\r\n", times.Select(pair => $"{pair.Key}: {Math.Round((1000.0 * pair.Value / (double)Stopwatch.Frequency))}ms"))}");

				turn++;
			}
		}
	}
}
namespace CodersOfTheCaribbean {
	public class RumBarrel : Entity {
		public override EntityType Type => EntityType.RumBarrel;

		public int Health { get; private set; }
		private int _health;

		public RumBarrel(string[] data) : base(data) {
		}

		public override void Update(string[] data) {
			base.Update(data);
			Health = int.Parse(data[4]);
		}

		public override void Save() {
			base.Save();
			_health = Health;
		}

		public override void Reset() {
			base.Reset();
			Health = _health;
		}

		public static RumBarrel CreateDummy(Coordinate position, int health) {
			return new RumBarrel(new[] { "-1", EntityType.RumBarrel.ToString(), position.X.ToString(), position.Y.ToString(), health.ToString() });
		}

		public override string ToString() {
			return $"{base.ToString()} HP: {Health}";
		}
	}
}

namespace CodersOfTheCaribbean {
	public class Ship : Entity {
		public override EntityType Type => EntityType.Ship;

		public string Owner { get; }

		public int Speed { get; set; }
		private int _speed;

		public int Health { get; private set; }
		private int _health;

		public Orientation Orientation { get; set; }
		private Orientation _orientation;

		public int CannonCooldown { get; set; }
		private int _cannonCooldown;

		public int MineCooldown { get; set; }
		private int _mineCooldown;

		public ShipAction Action { get; set; }
		private ShipAction _action;

		public Coordinate Bow => Position.Neighbor(Orientation);
		public Coordinate Stern => Position.Neighbor(Orientation.Opposite());


		public Orientation NewOrientation { get; set; }
		private Orientation _newOrientation;
		public Coordinate NewPosition { get; set; }
		private Coordinate _newPosition;
		public Coordinate NewBowCoordinate { get; set; }
		private Coordinate _newBowCoordinate;
		public Coordinate NewSternCoordinate { get; set; }
		private Coordinate _newSternCoordinate;
		public Coordinate NewBow => Position.Neighbor(NewOrientation);
		public Coordinate NewStern => Position.Neighbor(NewOrientation.Opposite());

		public int InitialHealth { get; private set; }

		public Ship(string[] data) : base(data) {
			// TODO: Detect getting stuck
			Owner = data[7];
		}

		public override void Update(string[] data) {
			base.Update(data);
			NewOrientation = Orientation = (Orientation)int.Parse(data[4]);
			NewPosition = Position;
			Speed = int.Parse(data[5]);
			Health = int.Parse(data[6]);
			CannonCooldown = Math.Max(CannonCooldown - 1, 0);
			MineCooldown = Math.Max(MineCooldown - 1, 0);
			Action = null;
		}

		public override void Save() {
			base.Save();
			_speed = Speed;
			_orientation = Orientation;
			_health = Health;
			_action = Action;
			_cannonCooldown = CannonCooldown;
			_mineCooldown = MineCooldown;

			_newOrientation = NewOrientation;
			_newPosition = NewPosition;
			_newBowCoordinate = NewBowCoordinate;
			_newSternCoordinate = NewSternCoordinate;
		}

		public override void Reset() {
			base.Reset();
			Speed = _speed;
			Orientation = _orientation;
			Health = _health;
			Action = _action;
			CannonCooldown = _cannonCooldown;
			MineCooldown = _mineCooldown;

			NewOrientation = _newOrientation;
			NewPosition = _newPosition;
			NewBowCoordinate = _newBowCoordinate;
			NewSternCoordinate = _newSternCoordinate;
		}

		public void UpdateInitialHealth(int initialHealth) {
			InitialHealth = initialHealth;
		}

		public void Apply(Solution solution, int depth) {
			ShipAction[] actions;
			if(solution.Actions.TryGetValue(Id, out actions)) {
				Action = actions.ElementAt(depth);
			} else {
				Console.Error.WriteLine($"Could not find action to apply for ship {Id}");
			}
		}

		public void MoveTo(Coordinate targetPosition) {
			var currentPosition = Position;

			if (currentPosition.Equals(targetPosition)) {
				Wait();
				return;
			}

			double targetAngle, angleStraight, anglePort, angleStarboard, centerAngle, anglePortCenter, angleStarboardCenter;

			switch (Speed) {
				/*case 2: {
					//Slower();
					Wait();
					break;
				}*/
				case 2:
				case 1: {
					// Suppose we've moved first
					currentPosition = currentPosition.Neighbor(Orientation);
					if (!currentPosition.IsInsideMap()) {
						Wait();
						break;
					}

					// Target reached at next turn
					if (currentPosition.Equals(targetPosition)) {
						Wait();
						break;
					}

					targetAngle = currentPosition.Angle(targetPosition);
					angleStraight = Math.Min(Math.Abs((int)Orientation - targetAngle), 6 - Math.Abs((int)Orientation - targetAngle));
					anglePort = Math.Min(Math.Abs(((int)Orientation + 1) - targetAngle),
						6 - Math.Abs(((int)Orientation - 5) - targetAngle));
					angleStarboard = Math.Min(Math.Abs(((int)Orientation + 5) - targetAngle),
						6 - Math.Abs(((int)Orientation - 1) - targetAngle));

					centerAngle = currentPosition.Angle(new Coordinate(Config.MAP_WIDTH/2, Config.MAP_HEIGHT/2));
					anglePortCenter = Math.Min(Math.Abs(((int)Orientation + 1) - centerAngle),
						6 - Math.Abs(((int)Orientation - 5) - centerAngle));
					angleStarboardCenter = Math.Min(Math.Abs(((int)Orientation + 5) - centerAngle),
						6 - Math.Abs(((int)Orientation - 1) - centerAngle));

					Console.Error.WriteLine($"straight angle: {angleStraight}");
					// Next to target with bad angle, slow down then rotate (avoid to turn around the target!)
					if ((currentPosition.DistanceTo(targetPosition) == 1 && Speed > 0 && angleStraight > 1.5) || (currentPosition.DistanceTo(targetPosition) < 3 && Speed == 2 && angleStraight != 0)) {
							Console.Error.WriteLine($"S: {Speed} D: {currentPosition.DistanceTo(targetPosition)}");
						Slower();
						break;
					}

					int? distanceMin = null;

					// Test forward
					var nextPosition = currentPosition.Neighbor(Orientation);
					if (nextPosition.IsInsideMap()) {
						distanceMin = nextPosition.DistanceTo(targetPosition);
						if (distanceMin > 2) {
							Faster();
						} else {
							Wait();
						}
					}

					// Test port
					nextPosition = currentPosition.Neighbor(Orientation.Next());
					//if (nextPosition.IsInsideMap()) {
						var distance = nextPosition.DistanceTo(targetPosition);
						if (!distanceMin.HasValue || distance < distanceMin || distance == distanceMin && anglePort < angleStraight - 0.5) {
							distanceMin = distance;
							if (Speed == 2) {
								Slower();
							} else {
								Port();
							}
						}
					//}

					// Test starboard
					nextPosition = currentPosition.Neighbor(Orientation.Prev());
					//if (nextPosition.IsInsideMap()) {
						distance = nextPosition.DistanceTo(targetPosition);
						if (!distanceMin.HasValue || distance < distanceMin ||
							(distance == distanceMin && angleStarboard < anglePort - 0.5 && Action?.Type == ShipActionType.PORT) ||
							(distance == distanceMin && angleStarboard < angleStraight - 0.5 && Action == null) ||
							(distance == distanceMin && Action?.Type == ShipActionType.PORT && angleStarboard == anglePort &&
							 angleStarboardCenter < anglePortCenter) ||
							(distance == distanceMin && Action?.Type == ShipActionType.PORT && angleStarboard == anglePort &&
							 angleStarboardCenter == anglePortCenter &&
							 (Orientation == Orientation.UpRight || Orientation == Orientation.DownLeft))) {
							distanceMin = distance;
							if (Speed == 2) {
								Slower();
							} else {
								Starboard();
							}
						}
					//}

					break;
				}
				case 0: {
					// Rotate ship towards target
					targetAngle = currentPosition.Angle(targetPosition);
					angleStraight = Math.Min(Math.Abs((int)Orientation - targetAngle), 6 - Math.Abs((int)Orientation - targetAngle));
					anglePort = Math.Min(Math.Abs(((int)Orientation + 1) - targetAngle),
						Math.Abs(((int)Orientation - 5) - targetAngle));
					angleStarboard = Math.Min(Math.Abs(((int)Orientation + 5) - targetAngle),
						Math.Abs(((int)Orientation - 1) - targetAngle));

					centerAngle = currentPosition.Angle(new Coordinate(Config.MAP_WIDTH/2, Config.MAP_HEIGHT/2));
					anglePortCenter = Math.Min(Math.Abs(((int)Orientation + 1) - centerAngle),
						Math.Abs(((int)Orientation - 5) - centerAngle));
					angleStarboardCenter = Math.Min(Math.Abs(((int)Orientation + 5) - centerAngle),
						Math.Abs(((int)Orientation - 1) - centerAngle));

					var forwardPosition = currentPosition.Neighbor(Orientation);

					Action = null;

					if (anglePort <= angleStarboard) {
						Port();
					}

					if (angleStarboard < anglePort || angleStarboard == anglePort && angleStarboardCenter < anglePortCenter ||
						angleStarboard == anglePort && angleStarboardCenter == anglePortCenter &&
						(Orientation == Orientation.UpRight || Orientation == Orientation.DownLeft)) {
						Starboard();
					}

					if (forwardPosition.IsInsideMap() && angleStraight <= anglePort && angleStraight <= angleStarboard) {
						Console.Error.WriteLine($"Straight: {angleStraight} Port: {anglePort} Starboard: {angleStarboard}");
						Faster();
					}

					break;
				}
			}
		}

		public void Faster() {
			Action = new ShipAction(ShipActionType.FASTER);
		}

		public void Slower() {
			Action = new ShipAction(ShipActionType.SLOWER);
		}

		public void Port() {
			Action = new ShipAction(ShipActionType.PORT);
		}

		public void Starboard() {
			Action = new ShipAction(ShipActionType.STARBOARD);
		}

		public void PlaceMine() {
			Action = new ShipAction(ShipActionType.MINE);
		}

		public void Wait() {
			Action = new ShipAction(ShipActionType.WAIT);
		}

		public bool IsAt(Coordinate coordinate) // TODO: Slightly changed from source, need to check if correct
			=> coordinate != null && (Stern.Equals(coordinate) || Bow.Equals(coordinate) || Position.Equals(coordinate));

		public bool NewBowIntersects(Ship otherShip) => NewBowCoordinate != null &&
														(NewBowCoordinate.Equals(otherShip.NewBowCoordinate) || NewBowCoordinate.Equals(otherShip.NewPosition) ||
														 NewBowCoordinate.Equals(otherShip.NewSternCoordinate));

		public bool NewBowIntersects(IEnumerable<Ship> otherShips)
			=> otherShips.Any(otherShip => this != otherShip && NewBowIntersects(otherShip));

		public bool NewPositionsIntersect(Ship otherShip) {
			var sternCollision = NewSternCoordinate != null &&
								 (NewSternCoordinate.Equals(otherShip.NewBowCoordinate) || NewSternCoordinate.Equals(otherShip.NewPosition) ||
								  NewSternCoordinate.Equals(otherShip.NewSternCoordinate));
			var centerCollision = NewPosition != null &&
								  (NewPosition.Equals(otherShip.NewBowCoordinate) || NewPosition.Equals(otherShip.NewPosition) ||
								   NewPosition.Equals(otherShip.NewSternCoordinate));
			return NewBowIntersects(otherShip) || sternCollision || centerCollision;
		}

		public bool NewPositionsIntersect(IEnumerable<Ship> otherShips)
			=> otherShips.Any(otherShip => this != otherShip && NewPositionsIntersect(otherShip));

		public void Damage(int health) {
			Health = Math.Max(Health - health, 0);
		}

		public void Heal(int health) {
			Health = Math.Min(Health + health, Config.MAX_SHIP_HEALTH);
		}

		public void Fire(Coordinate targetCoordinate) {
			if (Config.CANNONS_ENABLED) {
				Action = new ShipAction(ShipActionType.FIRE, targetCoordinate);
			}
		}

		public void OutputAction(ShipAction action) {
			Console.WriteLine(action);
		}

		public void OutputAction(Solution solution) {
			ShipAction[] actions;
			if (solution.Actions.TryGetValue(Id, out actions)) {
				Console.WriteLine(actions.First());
			} else {
				Console.Error.WriteLine($"Could not find action for ship {Id}");
				//Console.WriteLine($"{ShipActionType.WAIT}");
			}
		}

		public void OutputAction() {
			Console.WriteLine(Action);
		}

		public override string ToString() {
			return $"{base.ToString()} ORIENTATION: {Orientation} SPEED: {Speed} HP: {Health}";
		}
	}
}
namespace CodersOfTheCaribbean {
	public enum ShipActionType {
		WAIT,
		SLOWER,
		FASTER,
		PORT,
		STARBOARD,
		FIRE,
		MINE
	}

	public class ShipAction {
		public ShipActionType Type { get; }
		public Coordinate Target { get; }

		public ShipAction(ShipActionType type) {
			Type = type;
		}

		public ShipAction(ShipActionType type, Coordinate target) : this(type) {
			Target = target;
		}

		public override string ToString() {
			return $"{Type}{(Target != null ? " " : string.Empty)}{Target}";
		}
	}
}

namespace CodersOfTheCaribbean {
	public static class Simulator {
		public static int Sims = 0;

		private static void MoveCannonballs(GameState gameState, ICollection<Coordinate> cannonballExplosions) {
			gameState.Cannonballs.RemoveAll(cannonball => {
				if (cannonball.RemainingTurns == 0) {
					return true;
				}
				if (cannonball.RemainingTurns > 0) {
					cannonball.RemainingTurns--;
				}
				if (cannonball.RemainingTurns == 0) {
					cannonballExplosions.Add(cannonball.Position);
				}
				return false;
			});
		}

		private static void DecrementRum(GameState gameState) {
			foreach (var ship in gameState.Ships) {
				ship.Damage(1);
			}
		}

		private static void UpdateInitialRum(GameState gameState) {
			foreach (var ship in gameState.Ships) {
				ship.UpdateInitialHealth(ship.Health);
			}
		}

		private static void ApplyActions(GameState gameState) {
			foreach (var ship in gameState.AliveShips) {
				if (ship.CannonCooldown > 0) {
					ship.CannonCooldown--;
				}
				if (ship.MineCooldown > 0) {
					ship.MineCooldown--;
				}

				ship.NewOrientation = ship.Orientation;
				
				if (ship.Action != null) {
					switch (ship.Action.Type) {
						case ShipActionType.SLOWER: {
							if (ship.Speed > 0) {
								ship.Speed--;
							}
							break;
						}
						case ShipActionType.FASTER: {
							if (ship.Speed < Config.MAX_SHIP_SPEED) {
								ship.Speed++;
							}
							break;
						}
						case ShipActionType.PORT: {
							ship.NewOrientation = ship.Orientation.Next();
							break;
						}
						case ShipActionType.STARBOARD: {
							ship.NewOrientation = ship.Orientation.Prev();
							break;
						}
						case ShipActionType.FIRE: {
							//using (new DisposableStopwatch(t => Program.times.Increment("ApplyActionFire", t))) {
								var distance = ship.Bow.DistanceTo(ship.Action.Target);
								if (ship.Action.Target.IsInsideMap() && distance <= Config.FIRE_DISTANCE_MAX && ship.CannonCooldown == 0) {
									var travelTime = (int)(1 + Math.Round(ship.Bow.DistanceTo(ship.Action.Target)/3.0));
									ship.CannonCooldown = Config.COOLDOWN_CANNON;
									gameState.Cannonballs.Add(Cannonball.CreateDummy(ship.Action.Target, ship.Id, travelTime));
								}
							//}
							break;
						}
						case ShipActionType.MINE: {
							//using (new DisposableStopwatch(t => Program.times.Increment("ApplyActionMine", t))) {
								if (ship.MineCooldown == 0) {
									var target = ship.Stern.Neighbor(ship.Orientation.Opposite());

									if (target.IsInsideMap()) {
										var cellIsFreeOfBarrels = !gameState.Barrels.Any(barrel => barrel.Position.Equals(target));
										var cellIsFreeOfMines = !gameState.Mines.Any(mine => mine.Position.Equals(target));
										var cellIsFreeOfShips = !gameState.Ships.Any(otherShip => otherShip != ship && otherShip.IsAt(target));

										if (cellIsFreeOfBarrels && cellIsFreeOfMines && cellIsFreeOfShips) {
											ship.MineCooldown = Config.COOLDOWN_MINE;
											gameState.Mines.Add(Mine.CreateDummy(target));
										}
									}
								}
							//}
							break;
						}
					}
				}
			}
		}

		private static void CheckCollisions(GameState gameState, Ship ship) {
			//using (new DisposableStopwatch(t => Program.times.Increment("CheckCollisions", t))) {
				var bow = ship.Bow;
				var stern = ship.Stern;
				var center = ship.Position;

				//using (new DisposableStopwatch(t => Program.times.Increment("CheckBarrelCollisions", t))) {
					gameState.Barrels.RemoveAll(barrel => {
						if (barrel.Position.Equals(bow) || barrel.Position.Equals(stern) || barrel.Position.Equals(center)) {
							ship.Heal(barrel.Health);
							return true;
						}
						return false;
					});
				//}

				//using (new DisposableStopwatch(t => Program.times["CheckMineCollisions"] += t)) {
					//gameState.Mines.RemoveAll(mine => mine.Explode(gameState.Ships, false).Any());
				//}
			//}
		}

		private static void MoveShips(GameState gameState) {
			// Go forward
			for (var i = 1; i <= Config.MAX_SHIP_SPEED; i++) {
				foreach (var ship in gameState.Ships) {
					ship.NewPosition = ship.Position;
					ship.NewBowCoordinate = ship.Bow;
					ship.NewSternCoordinate = ship.Stern;

					if (i > ship.Speed) {
						continue;
					}

					var newCoordinate = ship.Position.Neighbor(ship.Orientation);

					if (newCoordinate.IsInsideMap()) {
						ship.NewPosition = newCoordinate;
						ship.NewBowCoordinate = newCoordinate.Neighbor(ship.Orientation);
						ship.NewSternCoordinate = newCoordinate.Neighbor(ship.Orientation.Opposite());
					} else {
						ship.Speed = 0;
					}
				}
				
				var collisionDetected = true;
				while (collisionDetected) {
					collisionDetected = false;

					foreach (var ship in gameState.Ships.Where(ship => ship.NewBowIntersects(gameState.Ships))) {
						// Revert last move
						ship.NewPosition = ship.Position;
						ship.NewBowCoordinate = ship.Bow;
						ship.NewSternCoordinate = ship.Stern;

						ship.Speed = 0;

						collisionDetected = true;
					}
				}

				foreach (var ship in gameState.Ships) {
					ship.Position = ship.NewPosition;
					/*using (new DisposableStopwatch(t => Program.times["MoveShipsCheckCollisions"] += t)) {
						CheckCollisions(gameState, ship);
					}*/
				}
			}
		}

		private static void RotateShips(GameState gameState) {
			foreach (var ship in gameState.Ships) {
				ship.NewPosition = ship.Position;
				ship.NewBowCoordinate = ship.NewBow;
				ship.NewSternCoordinate = ship.NewStern;
			}

			var collisionDetected = true;
			while(collisionDetected) {
				collisionDetected = false;

				foreach(var ship in gameState.Ships.Where(ship => ship.NewPositionsIntersect(gameState.Ships))) {
					// Revert last move
					ship.NewOrientation = ship.Orientation;
					ship.NewBowCoordinate = ship.NewBow;
					ship.NewSternCoordinate = ship.NewStern;

					ship.Speed = 0;

					collisionDetected = true;
				}
			}

			foreach(var ship in gameState.Ships) {
				ship.Orientation = ship.NewOrientation;
				/*using (new DisposableStopwatch(t => Program.times["RotateShipsCheckCollisions"] += t)) {
					CheckCollisions(gameState, ship);
				}*/
			}
		}

		private static void ExplodeShips(GameState gameState, List<Coordinate> cannonballExplosions) {
			cannonballExplosions.RemoveAll(position => {
				foreach (var ship in gameState.Ships) {
					if(position.Equals(ship.Bow) || position.Equals(ship.Stern)) {
						ship.Damage(Config.LOW_DAMAGE);
						return true;
					} else if (position.Equals(ship.Position)) {
						ship.Damage(Config.HIGH_DAMAGE);
						return true;
					}
				}
				return false;
			});
		}

		private static void ExplodeMines(GameState gameState, List<Coordinate> cannonballExplosions) {
			cannonballExplosions.RemoveAll(position => {
				var index = gameState.Mines.FindIndex(mine => mine.Position.Equals(position));
				if (index > -1) {
					gameState.Mines.RemoveAt(index);
					return true;
				}
				return false;
			});
		}

		private static void ExplodeBarrels(GameState gameState, List<Coordinate> cannonballExplosions) {
			cannonballExplosions.RemoveAll(position => {
				var index = gameState.Barrels.FindIndex(barrel => barrel.Position.Equals(position));
				if(index > -1) {
					gameState.Barrels.RemoveAt(index);
					return true;
				}
				return false;
			});
		}

		private static void SimulateTurn(GameState gameState) {
			var cannonballExplosions = new List<Coordinate>();
			//if (Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"Start turn sim at {Program.tempStopwatch.ElapsedMilliseconds}ms"); }
			//using(new DisposableStopwatch(t => Program.times.Increment("MoveCannonballs", t))) {
				MoveCannonballs(gameState, cannonballExplosions);
			//}
			DecrementRum(gameState);
//Console.Error.WriteLine($"2/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");
			UpdateInitialRum(gameState);
			//Console.Error.WriteLine($"3/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");
			//using (new DisposableStopwatch(t => Program.times.Increment("ApplyActions", t))) {
				ApplyActions(gameState);
			//}
			//using (new DisposableStopwatch(t => Program.times.Increment("MoveShips", t))) {
				MoveShips(gameState);
			//}
			//using (new DisposableStopwatch(t => Program.times.Increment("RotateShips", t))) {
				RotateShips(gameState);
				//}
			//using (new DisposableStopwatch(t => Program.times.Increment("CheckCollisions", t))) {
				foreach (var ship in gameState.Ships) {
					CheckCollisions(gameState, ship);
				}
			//}
			//Console.Error.WriteLine($"6/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");
			//using (new DisposableStopwatch(t => Program.times.Increment("ExplodeShips", t))) {
				ExplodeShips(gameState, cannonballExplosions);
			//}
			//if (Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"7/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");}
			//using (new DisposableStopwatch(t => Program.times.Increment("ExplodeMines", t))) {
				ExplodeMines(gameState, cannonballExplosions);
			//}
			//if(Program.tempStopwatch.ElapsedMilliseconds > 43 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"8/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");}
			//using (new DisposableStopwatch(t => Program.times.Increment("ExplodeBarrels", t))) {
				ExplodeBarrels(gameState, cannonballExplosions);
			//}
			//if(Program.tempStopwatch.ElapsedMilliseconds > 20 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"9/9 at {Program.tempStopwatch.ElapsedMilliseconds}ms");}
			//using (new DisposableStopwatch(t => Program.times.Increment("SpawnDummyBarrels", t))) {
				foreach (var ship in gameState.Ships.Where(ship => ship.Health <= 0)) {
					var reward = Math.Min(Config.REWARD_RUM_BARREL_VALUE, ship.InitialHealth);
					if (reward > 0) {
						gameState.Barrels.Add(RumBarrel.CreateDummy(ship.Position, reward));
					}
				}
			//}

			//using (new DisposableStopwatch(t => Program.times.Increment("RemoveDeadShips", t))) {
				gameState.Ships.RemoveAll(ship => ship.Health <= 0);
			//}
			Sims++;
			//Console.Error.WriteLine($"Done");
		}

		private static double WeighState(GameState gameState, bool shipDied) {
			//return Program.RANDOM.Next(50);
			double score = 0;

			foreach (var ship in gameState.MyShips) {
//if (Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"hp start at {Program.tempStopwatch.ElapsedMilliseconds}ms");}
				score += (ship.Health * 3);
				//var ahh = ship.Health;
//if(Program.tempStopwatch.ElapsedMilliseconds > 40 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"hp end at {Program.tempStopwatch.ElapsedMilliseconds}ms"); }

				score = gameState.Barrels.Aggregate(score, (current, barrel) => {
					return current + 100*barrel.Health - 100*ship.Position.DistanceTo(barrel.Position);
				});
			}

			/*foreach(var ship in gameState.EnemyShips) {
				score -= (ship.Health * 5);

				score = gameState.Barrels.Aggregate(score, (current, barrel) => {
					return current - 100*barrel.Health;// - 100*ship.Position.DistanceTo(barrel.Position);
				});
			}*/

			if (shipDied) {
				score -= 10000;
			}

			return score;
		}

		public static void Run(GameState gameState, Solution solution) {
//if(Program.tempTurn == 2 && Program.tempStopwatch.ElapsedMilliseconds > 15 && Program.tempStopwatch.ElapsedMilliseconds < 50) { Console.Error.WriteLine($"Sim solution at {Program.tempStopwatch.ElapsedMilliseconds}ms: {string.Join(" | ", solution.Actions.Values.First().ToList())}");}
			var shipDied = false;
			for (var i = 0; i < Config.SIM_DEPTH; i++) {
//Console.Error.WriteLine($"Turn {i}");
				foreach(var ship in gameState.MyAliveShips) {
					ship.Apply(solution, i);
				}
				// TODO: Calculate rudamentary enemy actions

				SimulateTurn(gameState);
				if (i == 0) {
					solution.Score = WeighState(gameState, shipDied) * 0.3;
				}
				shipDied = gameState.MyShips.Any(ship => ship.Health <= 0);
//Console.Error.WriteLine($"Turn {i} done");
			}
			//Console.Error.WriteLine($"All turns done");
			//using (new DisposableStopwatch(t => Program.times.Increment("WeighState", t))) {
			
			solution.Score += WeighState(gameState, shipDied);
			if(shipDied) {
				Console.Error.WriteLine("I'm DEAD");
				Console.Error.WriteLine(solution.Score);
				Console.Error.WriteLine(solution.ToString());
			}
			//}

			//Console.Error.WriteLine($"Score: {solution.Score}");
			gameState.Reset();
		}

		public static double Run(GameState gameState, IDictionary<string, IList<ShipAction>> actions) {
			double score = 0;
			for(var i = 0; i < actions.Values.First().Count(); i++) {
				foreach(var ship in gameState.MyShips) {
					ship.Action = actions[ship.Id][i];
				}
				// TODO: Calculate rudamentary enemy actions

				SimulateTurn(gameState);

				if (i == 0) {
					score += WeighState(gameState, false) * 0.2;
				}

				foreach (var ship in gameState.MyShips) {
					if (ship.Health <= 0) {
						gameState.Reset();
						return -10000;
					}
				}
			}
			
			score += WeighState(gameState, false);
			gameState.Reset();
			return score;
		}
	}
}

namespace CodersOfTheCaribbean {
	public class Solution {
		public double Score { get; set; }
		public IDictionary<string, ShipAction[]> Actions { get; set; }

		public Solution() {
			Score = 0;
			Actions = new Dictionary<string, ShipAction[]>();
		}

		public void Randomize(GameState gameState) {
			foreach (var ship in gameState.MyAliveShips) {
				ShipAction[] actions;
				if (!Actions.TryGetValue(ship.Id, out actions)) {
					actions = new ShipAction[Config.SIM_DEPTH];
					Actions.Add(ship.Id, actions);
				}

				for (var i = 0; i < Config.SIM_DEPTH; i++) {
					var actionType = typeof(ShipActionType).PickRandom();
					var target = new Coordinate(Program.RANDOM.Next(Config.MAP_WIDTH), Program.RANDOM.Next(Config.MAP_HEIGHT));
					actions[i] = new ShipAction(actionType, target);
				}
			}
		}

		public void Copy(Solution other) {
			Score = other.Score;
			Actions = new Dictionary<string, ShipAction[]>();
			foreach (var keyValuePair in other.Actions) {
				Actions.Add(keyValuePair.Key, keyValuePair.Value.Select(x => x != null ? new ShipAction(x.Type, x.Target) : null).ToArray());
			}
		}

		public Solution Merge(Solution other) {
			var child = new Solution();

			foreach (var keyValuePair in Actions) {
				var actions = new ShipAction[Config.SIM_DEPTH];

				for (var i = 0; i < Config.SIM_DEPTH; i++) {
					if (Program.RANDOM.Next(2) == 0 && other.Actions.ContainsKey(keyValuePair.Key)) {
						actions[i] = other.Actions[keyValuePair.Key][i];
					} else {
						actions[i] = Actions[keyValuePair.Key][i];
					}
				}

				child.Actions.Add(keyValuePair.Key, actions);
			}

			return child;
		}

		public void Mutate(GameState gameState) {
			foreach(var ship in gameState.MyAliveShips) {
				ShipAction[] actions;
				if(!Actions.TryGetValue(ship.Id, out actions)) {
					actions = new ShipAction[Config.SIM_DEPTH];
					Actions.Add(ship.Id, actions);
				}

				ShipActionType actionType;
				Coordinate target;
				var randomIndex = Program.RANDOM.Next(Config.SIM_DEPTH);
				if (Program.RANDOM.Next(2) == 0) {
					// Change action
					actionType = typeof(ShipActionType).PickRandom();
					target = actions[randomIndex].Target;
				} else {
					// Change target
					actionType = actions[randomIndex].Type;
					target = new Coordinate(Program.RANDOM.Next(Config.MAP_WIDTH), Program.RANDOM.Next(Config.MAP_HEIGHT));
				}
				actions[randomIndex] = new ShipAction(actionType, target);
			}
		}

		public override string ToString() {
			return string.Join("\r\n", Actions.Select(pair => $"Ship: {pair.Key}\r\n{string.Join("\r\n", pair.Value.ToList())}"));
		}
	}
}
