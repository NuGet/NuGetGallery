// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.Framework
{
    public static class MemberDataHelper
    {
        public static object[] AsData(params object[] data)
        {
            return data;
        }

        public static IEnumerable<object[]> AsDataSet(params object[] dataSet)
        {
            return dataSet.Select(d => new[] { d });
        }

        public static IEnumerable<object[]> Combine(params IEnumerable<object[]>[] dataSets)
        {
            if (!dataSets.Any())
            {
                yield break;
            }

            var firstDataSet = dataSets.First();
            var lastDataSet = Combine(dataSets.Skip(1).ToArray()).ToArray();
            foreach (var firstData in firstDataSet)
            {
                foreach (var lastData in lastDataSet)
                {
                    yield return firstData.Concat(lastData).ToArray();
                }

                if (!lastDataSet.Any())
                {
                    yield return firstData;
                }
            }
        }
    }
}
