using System;
using System.Collections.Generic;

namespace Bum.EventGrid.Subscriptions.Scanner
{
     /// <summary>
    /// A random number generator with a Uniform distribution that is thread-safe (via locking).
    /// Can be instantiated with a custom <see cref="int"/> seed to make it emit deterministically.
    /// </summary>
    internal sealed class ConcurrentRandom
    {
        // Singleton approach is per MS best-practices.
        // https://docs.microsoft.com/en-us/dotnet/api/system.random?view=netframework-4.7.2#the-systemrandom-class-and-thread-safety
        // https://stackoverflow.com/a/25448166/
        // Also note that in concurrency testing, using a 'new Random()' for every thread ended up
        // being highly correlated. On NetFx this is maybe due to the same seed somehow being used
        // in each instance, but either way the singleton approach mitigated the problem.
        private static readonly Random SRandom = new Random();
        private readonly Random _random;

        /// <summary>
        /// Creates an instance of the <see cref="ConcurrentRandom"/> class.
        /// </summary>
        /// <param name="seed">An optional <see cref="Random"/> seed to use.
        /// If not specified, will use a shared instance with a random seed, per Microsoft recommendation for maximum randomness.</param>
        public ConcurrentRandom(int? seed = null)
        {
            _random = seed == null
                ? SRandom // Do not use 'new Random()' here; in concurrent scenarios they could have the same seed
                : new Random(seed.Value);
        }

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to 0.0,
        /// and less than 1.0.
        /// This method uses locks in order to avoid issues with concurrent access.
        /// </summary>
        public double NextDouble()
        {
            // It is safe to lock on _random since it's not exposed
            // to outside use so it cannot be contended.
            lock (_random)
            {
                return _random.NextDouble();
            }
        }

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to <paramref name="a"/>,
        /// and less than <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The minimum value.</param>
        /// <param name="b">The maximum value.</param>
        public double Uniform(double a, double b)
        {

            if (a == b) return a;

            return a + (b - a) * NextDouble();
        }
    }

     public class BackoffStrategy
     {
         /// <summary>
         /// Generates sleep durations in an jittered manner, making sure to mitigate any correlations.
         /// For example: 117ms, 236ms, 141ms, 424ms, ...
         /// For background, see https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/.
         /// </summary>
         /// <param name="minDelay">The minimum duration value to use for the wait before each retry.</param>
         /// <param name="maxDelay">The maximum duration value to use for the wait before each retry.</param>
         /// <param name="retryCount">The maximum number of retries to use, in addition to the original call.</param>
         /// <param name="seed">An optional <see cref="Random"/> seed to use.
         /// If not specified, will use a shared instance with a random seed, per Microsoft recommendation for maximum randomness.</param>
         /// <param name="fastFirst">Whether the first retry will be immediate or not.</param>
         public static IEnumerable<TimeSpan> DecorrelatedJitterBackoff(TimeSpan minDelay, TimeSpan maxDelay,
             int retryCount, int? seed = null, bool fastFirst = false)
         {
             if (minDelay < TimeSpan.Zero)
                 throw new ArgumentOutOfRangeException(nameof(minDelay), minDelay, "should be >= 0ms");
             if (maxDelay < minDelay)
                 throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, $"should be >= {minDelay}");
             if (retryCount < 0)
                 throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "should be >= 0");

             if (retryCount == 0)
                 return Empty();

             return Enumerate(minDelay, maxDelay, retryCount, fastFirst, new ConcurrentRandom(seed));
         }

         private static IEnumerable<TimeSpan> Enumerate(TimeSpan min, TimeSpan max, int retry, bool fast,
             ConcurrentRandom random)
         {
             int i = 0;
             if (fast)
             {
                 i++;
                 yield return TimeSpan.Zero;
             }

             // https://github.com/aws-samples/aws-arch-backoff-simulator/blob/master/src/backoff_simulator.py#L45
             // self.sleep = min(self.cap, random.uniform(self.base, self.sleep * 3))

             // Formula avoids hard clamping (which empirically results in a bad distribution)
             double ms = min.TotalMilliseconds;
             for (; i < retry; i++)
             {
                 double ceiling = Math.Min(max.TotalMilliseconds, ms * 3);
                 ms = random.Uniform(min.TotalMilliseconds, ceiling);

                 yield return TimeSpan.FromMilliseconds(ms);
             }
         }

         private static IEnumerable<TimeSpan> Empty()
         {
             yield break;
         }
     }
}