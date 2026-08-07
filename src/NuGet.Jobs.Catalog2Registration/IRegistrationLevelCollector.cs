// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// The collector for the ID-level (package registration scoped) attribute lane. This is distinct from
    /// <see cref="NuGet.Services.V3.ICollector"/> (the version-level collector) so the two lanes can be resolved and
    /// wired independently.
    /// </summary>
    public interface IRegistrationLevelCollector
    {
        Task<bool> RunAsync(ReadWriteCursor front, ReadCursor back, CancellationToken cancellationToken);
    }
}
