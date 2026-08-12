// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IStagingService
    {
        Task<StagingUploadResult> UploadAsync(User currentUser, User owner, Credential credential, StagingUploadRequest request);

        StagedPackageResource GetPackage(StagingAuthorizationContext context, string id, string version);

        StagedPackageListResource ListPackages(StagingAuthorizationContext context, string groupId, bool ungrouped, int take, string continuationToken);

        Task<StagingDownloadResult> DownloadPackageAsync(StagingAuthorizationContext context, string id, string version);

        Task<StagingDownloadResult> DownloadSymbolsAsync(StagingAuthorizationContext context, string id, string version);

        Task<StagedPackageResource> SetListedAsync(StagingAuthorizationContext context, string id, string version, bool listed);

        Task DeletePackageAsync(StagingAuthorizationContext context, string id, string version);

        Task DeleteSymbolsAsync(StagingAuthorizationContext context, string id, string version);

        Task<StagingGroupResult> CreateGroupAsync(StagingAuthorizationContext context, StagingCreateGroupRequest request);

        IReadOnlyList<StagingGroupResource> ListGroups(StagingAuthorizationContext context);

        StagingGroupDetailResource GetGroup(StagingAuthorizationContext context, string groupId, int take, string continuationToken);

        Task<StagingGroupResource> RenameGroupAsync(StagingAuthorizationContext context, string groupId, StagingRenameGroupRequest request);

        Task DeleteGroupAsync(StagingAuthorizationContext context, string groupId);

        Task<StagedPackageResource> AddPackageToGroupAsync(StagingAuthorizationContext context, string groupId, string id, string version);

        Task<StagedPackageResource> RemovePackageFromGroupAsync(StagingAuthorizationContext context, string groupId, string id, string version);
    }
}
