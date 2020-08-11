// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Protocol.Catalog
{
    /// <summary>
    /// An interface which allows reading and writing a cursor value. The value is up to what point in the catalog
    /// has been successfully processed. The value is a catalog commit timestamp.
    /// </summary>
    public interface ICursor
    {
        /// <summary>
        /// Get the value of the cursor.
        /// </summary>
        /// <returns>The cursor value. Null if the cursor has no value yet.</returns>
        Task<DateTimeOffset?> GetAsync();

        /// <summary>
        /// Set the value of the cursor.
        /// </summary>
        /// <param name="value">The new cursor value.</param>
        Task SetAsync(DateTimeOffset value);
    }
}
