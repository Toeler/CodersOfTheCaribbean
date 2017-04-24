using System.Collections.Generic;
using System.Linq;

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
