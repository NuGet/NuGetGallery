// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public interface INewPackageRegistrationProducer
    {
        Task ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationToken cancellationToken);
    }
}