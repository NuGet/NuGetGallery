// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.V3.Support
{
    public static class IterTools
    {
        /// <summary>
        /// Source: https://stackoverflow.com/a/3098381
        /// </summary>
        public static IEnumerable<IEnumerable<Tuple<T, int>>> CombinationsOfTwoByIndex<T>(
            IEnumerable<T> sequenceA,
            IEnumerable<int> sequenceBCounts)
        {
            // This takes as input a sequence of elements (A) and a sequence of element counts (related to another
            // sequence B). The count at each position is how many elements from B to combine with that element of
            // A. Suppose the input is:
            //
            //   A = [ x, y, z ]
            //   B = [ 2, 2, 2 ]
            //
            // The output (in no particular order) would be:
            //
            //   [
            //     [ x-1, y-1, z-1 ], [ x-1, y-1, z-2 ],
            //     [ x-1, y-2, z-1 ], [ x-1, y-2, z-2 ],
            //     [ x-2, y-1, z-1 ], [ x-2, y-1, z-2 ],
            //     [ x-2, y-2, z-1 ], [ x-2, y-2, z-2 ],
            //   ]
            //
            // This allows the caller to index into sequence B and produce the combinations of A and B.
            return from cpLine in CartesianProduct(
                   from count in sequenceBCounts select Enumerable.Range(1, count))
                   select cpLine.Zip(sequenceA, (x1, x2) => Tuple.Create(x2, x1));

        }

        public static IEnumerable<IEnumerable<Tuple<T1, T2>>> CombinationsOfTwo<T1, T2>(
            IReadOnlyCollection<T1> sequenceA,
            IReadOnlyList<T2> sequenceB)
        {
            // This has the same behavior as CombinationsOfTwoByIndex but maps the sequence B indexes to actual
            // values. Suppose the input is:
            //
            //   A = [ x, y, z ]
            //   B = [ a, b ]
            //
            // The output (in no particular order) would be:
            //
            //   [
            //     [ x-a, y-a, z-a ], [ x-a, y-a, z-b ],
            //     [ x-a, y-b, z-a ], [ x-a, y-b, z-b ],
            //     [ x-b, y-a, z-a ], [ x-b, y-a, z-b ],
            //     [ x-b, y-b, z-a ], [ x-b, y-b, z-b ],
            //   ]
            //
            // This allows the caller to create combinations of A and B where A is fixed but B is varied per
            // returned combination.
            var arr2 = Enumerable.Repeat(sequenceB.Count, sequenceA.Count);
            var combinations = CombinationsOfTwoByIndex(sequenceA, arr2);
            return combinations.Select(x => x.Select(t => Tuple.Create(t.Item1, sequenceB[t.Item2 - 1])));
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/3098381
        /// </summary>
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item })
                );
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/999182
        /// </summary>
        public static IEnumerable<IEnumerable<T>> SubsetsOf<T>(IEnumerable<T> source)
        {
            // This produces all subsets of the input. This includes the input itself and the empty set. The term
            // "set" is used to emphasize that order does not matter. The input is assumed to have unique items. If
            // it has duplicates, some output sets will also have duplicates.
            if (!source.Any())
            {
                return Enumerable.Repeat(Enumerable.Empty<T>(), 1);
            }

            var element = source.Take(1);

            var haveNots = SubsetsOf(source.Skip(1));
            var haves = haveNots.Select(set => element.Concat(set));

            return haves.Concat(haveNots);
        }
    }
}
