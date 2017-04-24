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
