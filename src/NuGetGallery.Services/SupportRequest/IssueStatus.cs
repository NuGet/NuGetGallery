// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public partial class IssueStatus
        : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Key { get; set; }

        [Index]
        [StringLength(200)]
        [Display(Description = "Issue Status")]
        public string Name { get; set; }

        // Navigation Properties
        public virtual Collection<Issue> Issues { get; set; }
    }
}
