using System.Collections.Generic;
using System.Linq;

namespace Bum.EventGrid.Subscriptions.Scanner
{
    public static class EnumerableExtensions
    {
        public static bool None<T>(this IEnumerable<T> enumerable) => !enumerable.Any();
        public static bool NullOrNone<T>(this IEnumerable<T> enumerable) => enumerable == null || enumerable.None();
    }
}