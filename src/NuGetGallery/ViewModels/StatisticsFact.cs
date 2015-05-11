// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;

namespace NuGetGallery
{
    public class StatisticsFact
    {
        public StatisticsFact(IDictionary<string, string> dimensions, int amount)
        {
            Dimensions = new Dictionary<string, string>(dimensions);
            Amount = amount;
        }

        public IDictionary<string, string> Dimensions { get; private set; }
        public int Amount { get; private set; }
    }
}