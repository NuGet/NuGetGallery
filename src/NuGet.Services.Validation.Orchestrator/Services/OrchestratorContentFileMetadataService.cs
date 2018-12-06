// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    class OrchestratorContentFileMetadataService : IContentFileMetadataService
    {
        public OrchestratorContentFileMetadataService()
        {
        }

        public string PackageContentFolderName => CoreConstants.Folders.FlatContainerFolderName;

        public string PackageContentPathTemplate => CoreConstants.PackageContentFileSavePathTemplate;
    }
}
