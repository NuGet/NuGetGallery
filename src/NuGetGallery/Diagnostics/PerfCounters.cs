using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Diagnostics
{
    /// <summary>
    /// Super simple in-process perf-counters (because Windows Perf Counters are le hard)
    /// </summary>
    public static class PerfCounters
    {
        private static ConcurrentDictionary<string, PerfCounter> _counters = new ConcurrentDictionary<string, PerfCounter>(StringComparer.OrdinalIgnoreCase);

        public static void AddSample(string name, int sampleSize, double value)
        {
            var counter = _counters.GetOrAdd(name, _ => new PerfCounter(sampleSize));
            counter.AddSample(value);
        }

        public static PerfStats GetStats(string name)
        {
            PerfCounter c;
            if (!_counters.TryGetValue(name, out c))
            {
                return null;
            }
            return c.GetStats();
        }

        private class PerfCounter
        {
            private int _position = 0;
            private double[] _ring;
            private int _count;
            private object _lock = new object(); // Locking :(

            public PerfCounter(int sampleSize)
            {
                _position = 0;
                _ring = new double[sampleSize];
            }

            public void AddSample(double value)
            {
                lock (_lock)
                {
                    // Ye olde ring buffer :)
                    _ring[_position] = value;
                    _position = (_position + 1) % _ring.Length;
                    
                    if (_count < _ring.Length)
                    {
                        _count++;
                    }
                }
            }

            public PerfStats GetStats()
            {
                lock (_lock)
                {
                    if (_count == 0)
                    {
                        return null;
                    }

                    double sum = 0;
                    double min = Double.MaxValue;
                    double max = Double.MinValue;

                    // Start at _position-1 and work backwards. 
                    // In order to avoid negative indicies, we do this by ADDING the distance between the end of ring and i, 
                    //  then we use mod to get a real offset
                    for (int i = 0; i < _count; i++)
                    {
                        double val = _ring[((_position - 1) + (_ring.Length - i)) % _ring.Length];
                        sum += val;
                        min = Math.Min(min, val);
                        max = Math.Max(max, val);
                    }

                    var avg = sum / _count;
                    return new PerfStats(
                        avg,
                        max,
                        min,
                        _count);
                }
            }
        }

        public class PerfStats
        {
            public double Average { get; private set; }
            public double Max { get; private set; }
            public double Min { get; private set; }
            public int Samples { get; private set; }

            public PerfStats(double average, double max, double min, int samples)
            {
                Average = average;
                Max = max;
                Min = min;
                Samples = samples;
            }
        }
    }
}