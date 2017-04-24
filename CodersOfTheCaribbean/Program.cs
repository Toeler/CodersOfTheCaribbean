using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
