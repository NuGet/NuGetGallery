// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class StatisticsFact
    {
        public StatisticsFact(IDictionary<string, string> dimensions, long amount)
        {
            Dimensions = new Dictionary<string, string>(dimensions);
            Amount = amount;
        }

        public IDictionary<string, string> Dimensions { get; private set; }
        public long Amount { get; private set; }
    }
}