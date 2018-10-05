// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status
{
    /// <summary>
    /// Represents a part of the service that has a status.
    /// </summary>
    public interface IReadOnlyComponent : IComponentDescription, IRootComponent<IReadOnlyComponent>
    {
    }
}
