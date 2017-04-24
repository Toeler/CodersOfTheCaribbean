using System;
using System.Diagnostics;

namespace CodersOfTheCaribbean {
	public class DisposableStopwatch : IDisposable {
		private readonly Stopwatch _sw;
		private readonly Action<double> _fTick;
		private readonly Action<TimeSpan> _fMs;

		public DisposableStopwatch(Action<double> f) {
			_fTick = f;
			_sw = Stopwatch.StartNew();
		}

		public DisposableStopwatch(Action<TimeSpan> f) {
			_fMs = f;
			_sw = Stopwatch.StartNew();
		}

		public void Dispose() {
			_sw.Stop();
			if (_fTick != null) {
				//_fTick(((double)_sw.ElapsedTicks / Stopwatch.Frequency) * 1000000000.0);
				_fTick(_sw.ElapsedTicks);
			}
			if(_fMs != null) {
				_fMs(_sw.Elapsed);
			}
		}
	}
}
