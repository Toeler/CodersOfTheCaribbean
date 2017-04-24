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
