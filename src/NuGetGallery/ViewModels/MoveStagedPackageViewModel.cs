// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class MoveStagedPackageViewModel
    {
        public string Owner { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        [StringLength(64)]
        public string GroupId { get; set; }

        public IReadOnlyList<StagingGroupAssignmentViewModel> Groups { get; set; }
    }

    public class StagingGroupAssignmentViewModel
    {
        public string Id { get; set; }

        public string Name { get; set; }
    }
}
