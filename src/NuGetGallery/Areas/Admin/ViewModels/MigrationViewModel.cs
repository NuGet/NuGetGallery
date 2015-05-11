// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class MigrationViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Description { get; set; }
        public DateTime CreatedLocal { get { return CreatedUtc.ToLocalTime(); } }
    }
}