// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackageIdentifier(string id, string version)
    {
        public string Id { get; } = id;
        public string Version { get; } = version;
    }
}