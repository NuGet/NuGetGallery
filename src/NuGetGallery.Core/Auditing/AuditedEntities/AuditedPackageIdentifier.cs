// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedPackageIdentifier
    {
        public string Id { get; }
        public string Version { get; }

        public AuditedPackageIdentifier(string id, string version)
        {
            Id = id;
            Version = version;
        }
    }
}