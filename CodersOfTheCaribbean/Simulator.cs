using System;
using System.Collections.Generic;
using System.Linq;

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
