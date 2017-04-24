namespace CodersOfTheCaribbean {
	public static class Config {
		public const int SEED = 42;

		public const int TURN_1_LIMIT = 1000;
		public const int TURN_LIMIT = 50;

		public const string OWNER_ID = "1";

		public const int MAP_WIDTH = 23;
		public const int MAP_HEIGHT = 21;

		public const int MAX_SHIP_HEALTH = 100;
		public const int MAX_SHIP_SPEED = 2;

		public const bool CANNONS_ENABLED = true;
		public const int COOLDOWN_CANNON = 2;
		public const int FIRE_DISTANCE_MAX = 10;
		public const int HIGH_DAMAGE = 50;
		public const int LOW_DAMAGE = 25;

		public const bool MINES_ENABLED = true;
		public const int COOLDOWN_MINE = 5;
		public const int MINE_DAMAGE = 25;
		public const int NEAR_MINE_DAMAGE = 10;

		public const int REWARD_RUM_BARREL_VALUE = 30;

		public const int SIM_DEPTH = 5;
		public const int POOL_SIZE = 50;
		public const int MUTATION = 2;
	}
}
