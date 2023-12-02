// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageOwnershipChangesModel
    {
        public PackageOwnershipChangesModel(
            PackageOwnershipChangesInput input,
            User requestor,
            IReadOnlyList<string> addOwners,
            IReadOnlyList<string> removeOwners,
            IReadOnlyList<PackageRegistrationOwnershipChangeModel> changes)
        {
            Input = input;
            Requestor = requestor;
            AddOwners = addOwners;
            RemoveOwners = removeOwners;
            Changes = changes;
        }

        public PackageOwnershipChangesInput Input { get; }
        public User Requestor { get; }
        public string RequestorUsername => Requestor.Username;
        public IEnumerable<string> PackageIds => Changes.Select(x => x.Id);
        public IReadOnlyList<string> AddOwners { get; }
        public IReadOnlyList<string> RemoveOwners { get; }
        public string Message => Input.Message;
        public bool SkipRequestFlow => Input.SkipRequestFlow;
        public IReadOnlyList<PackageRegistrationOwnershipChangeModel> Changes { get; }
    }
}