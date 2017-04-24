using System;

namespace CodersOfTheCaribbean {
	public class CubeCoordinate {
		private static readonly int[][] DIRECTIONS = {
			new[] {  1, -1,  0 },
			new[] {  1,  0, -1 },
			new[] {  0,  1, -1 },
			new[] { -1,  1,  0 },
			new[] { -1,  0,  1 },
			new[] {  0, -1,  1 }
		};

		private int X { get; }
		private int Y { get; }
		private int Z { get; }

		public CubeCoordinate(int x, int y, int z) {
			X = x;
			Y = y;
			Z = z;
		}

		public Coordinate ToOffsetCoordinate() {
			var newX = (X + (Z - (Z & 1))/2);
			var newY = Z;
			return new Coordinate(newX, newY);
		}

		public CubeCoordinate Neighbor(Orientation orientation) {
			var directions = DIRECTIONS[(int)orientation];
			var nx = (X + directions[0]);
			var ny = (Y + directions[1]);
			var nz = (Z + directions[2]);
			return new CubeCoordinate(nx, ny, nz);
		}

		public int DistanceTo(CubeCoordinate coordinate) {
			return (Math.Abs(X - coordinate.X) + Math.Abs(Y - coordinate.Y) + Math.Abs(Z - coordinate.Z))/2;
		}

		public int DistanceTo(Coordinate coordinate) {
			return DistanceTo(coordinate.ToCubeCoordinate());
		}

		public override string ToString() {
			return $"{X} {Y} {Z}";
		}
	}
}
