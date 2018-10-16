// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Status.Table;

namespace StatusAggregator.Messages
{
    public interface IIncidentGroupMessageFilter
    {
        /// <summary>
        /// Returns whether or not messages should be posted about <paramref name="group"/> at time <paramref name="cursor"/>.
        /// </summary>
        bool CanPostMessages(IncidentGroupEntity group, DateTime cursor);
    }
}