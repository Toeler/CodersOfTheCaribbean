using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
