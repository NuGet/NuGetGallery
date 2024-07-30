// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Registration
{
    public interface ICommitted
    {
        string CommitId { get; set; }
        DateTimeOffset CommitTimestamp { get; set; }
    }
}
