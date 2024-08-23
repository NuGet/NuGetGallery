// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public interface IValidatorIdentity
    {
        /// <summary>
        /// Human readable name that represents the identity of the validation that was run.
        /// </summary>
        string Name { get; }
    }
}
