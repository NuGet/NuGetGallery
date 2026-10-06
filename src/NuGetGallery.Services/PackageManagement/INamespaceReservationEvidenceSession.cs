// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public interface INamespaceReservationEvidenceSession
    {
        Task<JObject> ExecuteAsync(string toolName, JObject arguments, CancellationToken cancellationToken);
    }
}