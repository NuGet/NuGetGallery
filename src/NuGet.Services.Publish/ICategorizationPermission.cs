// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
namespace NuGet.Services.Publish
{
    public interface ICategorizationPermission
    {
        Task<bool> IsAllowedToSpecifyCategory(string id);
    }
}