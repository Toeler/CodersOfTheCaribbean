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
