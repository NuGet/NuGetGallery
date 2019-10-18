// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Validation.Common.Job.Tests.Leases
{
    [CollectionDefinition(nameof(BlobStorageCollection))]
    public class BlobStorageCollection : ICollectionFixture<BlobStorageFixture>
    {
    }
}
