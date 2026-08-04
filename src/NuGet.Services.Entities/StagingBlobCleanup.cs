// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagingBlobCleanup : IEntity
    {
        public int Key { get; set; }

        [Required]
        [StringLength(256)]
        public string BlobPath { get; set; }

        [Required]
        [StringLength(256)]
        public string ExpectedETag { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
