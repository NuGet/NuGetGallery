// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    /// <summary>
    /// Maintains the current progress of the job.
    /// </summary>
    public interface ICursor
    {
        Task<DateTime> Get(string name);
        Task Set(string name, DateTime value);
    }
}
