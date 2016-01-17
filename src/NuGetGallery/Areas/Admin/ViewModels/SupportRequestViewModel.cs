// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class SupportRequestViewModel
    {
        // Readonly fields
        public string AssignedToGalleryUsername { get; set; }
        public string IssueStatusName { get; set; }
        public Issue Issue { get; set; }

        // Editable fields
        public int? AssignedTo { get; set; }
        public int IssueStatusId { get; set; }
    }
}