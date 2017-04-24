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
