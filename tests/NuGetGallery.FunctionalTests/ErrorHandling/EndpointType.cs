// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.FunctionalTests.ErrorHandling
{
    /// <summary>
    /// The possible simulated error endpoints. This is used with <see cref="SimulatedErrorType"/>.
    /// </summary>
    public enum EndpointType
    {
        Pages,
        Api,
        OData,
    }
}