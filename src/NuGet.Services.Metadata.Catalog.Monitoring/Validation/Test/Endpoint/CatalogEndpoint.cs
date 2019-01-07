// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the catalog blobs endpoint, which is the transaction log that all of V3 is based off.
    /// </summary>
    /// <remarks>
    /// Validations associated with this class should be primarily based off <see cref="ValidationContext.Entries"/> or <see cref="ValidationContext.DeletionAuditEntries"/>.
    /// </remarks>
    public class CatalogEndpoint : IEndpoint
    {
        /// <remarks>
        /// Technically, the catalog has a cursor, but we are using the max value to represent it because if a catalog entry exists, then it must be able to be validated against.
        /// We could fetch the cursor from the catalog and then verify that all catalog entries are before it, but that would be unnecessary.
        /// </remarks>
        public ReadCursor Cursor => new MemoryCursor(MemoryCursor.MaxValue);
    }
}
