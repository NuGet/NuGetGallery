// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class StagingUploadResult
    {
        public StagingUploadResult(StagedPackageResource package, bool created)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Created = created;
        }

        public StagedPackageResource Package { get; }
        public bool Created { get; }
    }
}
