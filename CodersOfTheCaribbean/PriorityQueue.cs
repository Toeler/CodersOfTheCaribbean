using System;
using System.Collections;
using System.Collections.Generic;

namespace CodersOfTheCaribbean {
	public class PriorityQueueItem<T> : IComparable<PriorityQueueItem<T>> {
		public T Item { get; set; }
		public double Priority { get; set; }

		public PriorityQueueItem(T item, double priority) {
			Item = item;
			Priority = priority;
		}

		public int CompareTo(PriorityQueueItem<T> other) {
			return Priority.CompareTo(other.Priority);
		}
	}

	/// <summary>
	/// A simplified priority queue implementation.  Is stable, auto-resizes, and thread-safe, at the cost of being slightly slower than
	/// FastPriorityQueue
	/// </summary>
	/// <typeparam name="TItem">The type to enqueue</typeparam>
	/// <typeparam name="TPriority">The priority-type to use for nodes.  Must extend IComparable&lt;TPriority&gt;</typeparam>
	public class PriorityQueue<T> where T : IComparable<T> {
		private List<T> data;

		public PriorityQueue() {
			this.data = new List<T>();
		}

		public void Enqueue(T item) {
			data.Add(item);
			int ci = data.Count - 1; // child index; start at end
			while(ci > 0) {
				int pi = (ci - 1) / 2; // parent index
				if(data[ci].CompareTo(data[pi]) >= 0) break; // child item is larger than (or equal) parent so we're done
				T tmp = data[ci]; data[ci] = data[pi]; data[pi] = tmp;
				ci = pi;
			}
		}

		public T Dequeue() {
			// assumes pq is not empty; up to calling code
			int li = data.Count - 1; // last index (before removal)
			T frontItem = data[0];   // fetch the front
			data[0] = data[li];
			data.RemoveAt(li);

			--li; // last index (after removal)
			int pi = 0; // parent index. start at front of pq
			while(true) {
				int ci = pi * 2 + 1; // left child index of parent
				if(ci > li) break;  // no children so done
				int rc = ci + 1;     // right child
				if(rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
					ci = rc;
				if(data[pi].CompareTo(data[ci]) <= 0) break; // parent is smaller than (or equal to) smallest child so done
				T tmp = data[pi]; data[pi] = data[ci]; data[ci] = tmp; // swap parent and child
				pi = ci;
			}
			return frontItem;
		}

		public T Peek() {
			T frontItem = data[0];
			return frontItem;
		}

		public int Count() {
			return data.Count;
		}

		public override string ToString() {
			string s = "";
			for(int i = 0; i < data.Count; ++i)
				s += data[i].ToString() + " ";
			s += "count = " + data.Count;
			return s;
		}

		public bool IsConsistent() {
			// is the heap property true for all data?
			if(data.Count == 0) return true;
			int li = data.Count - 1; // last index
			for(int pi = 0; pi < data.Count; ++pi) // each parent index
			{
				int lci = 2 * pi + 1; // left child index
				int rci = 2 * pi + 2; // right child index

				if(lci <= li && data[pi].CompareTo(data[lci]) > 0) return false; // if lc exists and it's greater than parent then bad.
				if(rci <= li && data[pi].CompareTo(data[rci]) > 0) return false; // check the right child too.
			}
			return true;
		}
	}
}