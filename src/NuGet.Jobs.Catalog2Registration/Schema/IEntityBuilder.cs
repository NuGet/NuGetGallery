// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    public interface IEntityBuilder
    {
        void UpdateLeafItem(RegistrationLeafItem existing, HiveType hive, string id, PackageDetailsCatalogLeaf packageDetails);
        RegistrationLeaf NewLeaf(RegistrationLeafItem leafItem);
        void UpdateCommit(ICommitted committed, CatalogCommit commit);
        void UpdateInlinedPageItem(RegistrationPage pageItem, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper);
        void UpdateNonInlinedPageItem(RegistrationPage pageItem, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper);
        void UpdatePage(RegistrationPage pageItem, HiveType hive, string id, int count, NuGetVersion lower, NuGetVersion upper);
        void UpdateIndex(RegistrationIndex index, HiveType hive, string id, int count);
        void UpdateIndexUrls(RegistrationIndex index, HiveType fromHive, HiveType toHive);
        void UpdatePageUrls(RegistrationPage page, HiveType fromHive, HiveType toHive);
        void UpdateLeafUrls(RegistrationLeaf leaf, HiveType fromHive, HiveType toHive);
    }
}