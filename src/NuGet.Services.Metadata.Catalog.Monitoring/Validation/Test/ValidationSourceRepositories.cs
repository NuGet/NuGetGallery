// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Exposes NuGet client APIs to be used in validations.
    /// </summary>
    public class ValidationSourceRepositories
    {
        public ValidationSourceRepositories(
            SourceRepository v2,
            SourceRepository v3)
        {
            V2 = v2 ?? throw new ArgumentNullException(nameof(v2));
            V3 = v3 ?? throw new ArgumentNullException(nameof(v3));
        }

        /// <summary>
        /// The <see cref="SourceRepository"/> that can be used to get V2 resources.
        /// </summary>
        public SourceRepository V2 { get; }

        /// <summary>
        /// The <see cref="SourceRepository"/> that can be used to get V3 resources.
        /// </summary>
        public SourceRepository V3 { get; }
    }
}
