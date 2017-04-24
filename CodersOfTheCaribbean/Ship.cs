using System;
using System.Collections.Generic;
using System.Linq;

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
