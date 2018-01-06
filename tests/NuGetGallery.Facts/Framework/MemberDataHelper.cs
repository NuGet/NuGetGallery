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

        public static IEnumerable<object[]> Combine(IEnumerable<object[]> firstDataSet, IEnumerable<object[]> secondDataSet)
        {
            foreach (var firstData in firstDataSet)
            {
                foreach (var secondData in secondDataSet)
                {
                    yield return firstData.Concat(secondData).ToArray();
                }
            }
        }
    }
}
