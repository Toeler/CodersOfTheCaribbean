namespace CodersOfTheCaribbean {
	public class Damage {
		public Coordinate Position { get; }
		public int Health { get; }
		public bool Hit { get; }

		public Damage(Coordinate position, int health, bool hit) {
			Position = position;
			Health = health;
			Hit = hit;
		}
	}
}
