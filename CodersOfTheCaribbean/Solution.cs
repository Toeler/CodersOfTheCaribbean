using System;
using System.Collections.Generic;
using System.Linq;

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
