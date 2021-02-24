// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Jobs.Validation
{
    public interface ISharedAccessSignatureService
    {
        /// <summary>
        /// Generates a new sas token from a sas definition.
        /// </summary>
        /// <param name="sasDefinition">The sas definition stored on key vault.</param>
        /// <returns>A new sas token from a sas definition stored on key vault.</returns>
        Task<string> GetFromManagedStorageAccountAsync(string sasDefinition);
    }
}