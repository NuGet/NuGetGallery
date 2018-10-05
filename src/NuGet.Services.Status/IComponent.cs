// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Services.Status
{
    /// <summary>
    /// A part of the service that has a writable status.
    /// </summary>
    public interface IComponent : IComponentDescription, IRootComponent<IComponent>
    {
        /// <summary>
        /// The status of this part of the service.
        /// </summary>
        new ComponentStatus Status { get; set; }

        [JsonIgnore]
        bool DisplaySubComponents { get; }
    }
}
