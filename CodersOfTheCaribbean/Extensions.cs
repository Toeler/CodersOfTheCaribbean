using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CodersOfTheCaribbean {
	public static class Extensions {
		/*public static T Next<T>(this T src) where T : struct {
			if(!typeof(T).IsEnum) {
				throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");
			}

			var arr = (T[])Enum.GetValues(src.GetType());
			var j = Array.IndexOf(arr, src) + 1;
			return arr.Length == j ? arr[0] : arr[j];
		}

		public static T Prev<T>(this T src) where T : struct {
			if(!typeof(T).IsEnum) {
				throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");
			}

			var arr = (T[])Enum.GetValues(src.GetType());
			var j = Array.IndexOf(arr, src) - 1;
			return j == -1 ? arr[arr.Length - 1] : arr[j];
		}*/

		public static Orientation Next(this Orientation src) {
			return (Orientation)(((int)src + 1)%6);
		}

		public static Orientation Prev(this Orientation src) {
			return (Orientation)(((int)src + 5) % 6);
		}

		public static Orientation Opposite(this Orientation src) {
			return (Orientation)(((int)src + 3)%6);
			var arr = (Orientation[])Enum.GetValues(typeof(Orientation));
			var j = Array.IndexOf(arr, src) + 3;
			return arr[j%arr.Length];
		}

		private static readonly IDictionary<EntityType, string> ENTITY_TYPE_TO_STRING = new Dictionary<EntityType, string> {
			{ EntityType.Cannonball, "CANNONBALL" },
			{ EntityType.Mine, "MINE" },
			{ EntityType.RumBarrel, "BARREL" },
			{ EntityType.Ship, "SHIP" }
		};
		public static string ToString(this EntityType src) {
			string value;
			ENTITY_TYPE_TO_STRING.TryGetValue(src, out value);
			return value;
		}
		private static readonly IDictionary<string, EntityType> STRING_TO_ENTITY_TYPE = new Dictionary<string, EntityType> {
			{ "CANNONBALL", EntityType.Cannonball },
			{ "MINE", EntityType.Mine },
			{ "BARREL", EntityType.RumBarrel },
			{ "SHIP", EntityType.Ship }
		};
		public static EntityType FromString(this Type t, string str) {
			EntityType value;
			STRING_TO_ENTITY_TYPE.TryGetValue(str, out value);
			return value;
		}

		public static ShipActionType PickRandom(this Type t) {
			var values = Enum.GetValues(t);
			return (ShipActionType)values.GetValue(Program.RANDOM.Next(values.Length));
		}

		public static double Increment(this ConcurrentDictionary<string, double> d, string key, double value) {
			return d.AddOrUpdate(key, value, (id, val) => val + value);
		}

		public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences) {
			IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
			return sequences.Aggregate(
			  emptyProduct,
			  (accumulator, sequence) =>
				from accseq in accumulator
				from item in sequence
				select accseq.Concat(new[] { item }));
		}

		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while(n > 1) {
				n--;
				int k = Program.RANDOM.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}
	}
}
