// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The config passed to <see cref="IEndpoint"/>s.
    /// </summary>
    public sealed class EndpointConfiguration
    {
        public EndpointConfiguration(
            Uri registrationCursorUri,
            Uri flatContainerCursorUri,
            IReadOnlyDictionary<string, SearchEndpointConfiguration> instanceNameToSearchConfig)
        {
            RegistrationCursorUri = registrationCursorUri ?? throw new ArgumentNullException(nameof(registrationCursorUri));
            FlatContainerCursorUri = flatContainerCursorUri ?? throw new ArgumentNullException(nameof(flatContainerCursorUri));
            InstanceNameToSearchConfiguration = instanceNameToSearchConfig ?? throw new ArgumentNullException(nameof(instanceNameToSearchConfig));
        }

        public Uri RegistrationCursorUri { get; }
        public Uri FlatContainerCursorUri { get; }
        public IReadOnlyDictionary<string, SearchEndpointConfiguration> InstanceNameToSearchConfiguration { get; }
    }
}
