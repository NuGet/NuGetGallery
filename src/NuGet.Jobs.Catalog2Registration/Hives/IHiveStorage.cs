// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Protocol.Registration;
using NuGet.Versioning;

namespace NuGet.Jobs.Catalog2Registration
{
    public interface IHiveStorage
    {
        Task DeleteIndexAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string id);
        Task DeleteUrlAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string url);
        Task<RegistrationIndex> ReadIndexOrNullAsync(HiveType hive, string id);
        Task<RegistrationPage> ReadPageAsync(HiveType hive, string url);
        Task WriteIndexAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string id, RegistrationIndex index);
        Task WriteLeafAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string id, NuGetVersion version, RegistrationLeaf leaf);
        Task WritePageAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string id, NuGetVersion lower, NuGetVersion upper, RegistrationPage page);
    }
}