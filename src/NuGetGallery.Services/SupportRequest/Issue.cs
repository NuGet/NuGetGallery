// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NuGet.Services.Entities;

namespace NuGetGallery.Services.SupportRequest
{
    public partial class Issue : IEntity
    {
        [Key]
        public int Key { get; set; }

        [StringLength(50)]
        public string CreatedBy { get; set; }

        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; }

        [Required(ErrorMessage = "Please provide an issue title")]
        [StringLength(1000)]
        public string IssueTitle { get; set; }

        [Required(ErrorMessage = "Please provide the details of the issue")]
        public string Details { get; set; }

        [DataType(DataType.Url)]
        public string SiteRoot { get; set; }

        [StringLength(300)]
        public string PackageId { get; set; }

        [StringLength(300)]
        public string PackageVersion { get; set; }

        [Required(ErrorMessage = "Please provide the owner email address")]
        [StringLength(100)]
        [DataType(DataType.EmailAddress)]
        public string OwnerEmail { get; set; }

        [StringLength(100)]
        public string Reason { get; set; }

        // Foreign Keys
        public int? AssignedToId { get; set; }
        public int IssueStatusId { get; set; }

        // (soft) foreign Key to main gallery db
        public int? PackageRegistrationKey { get; set; }
        public int? UserKey { get; set; }

        // Navigation properties
        public virtual Admin AssignedTo { get; set; }
        public virtual IssueStatus IssueStatus { get; set; }
        public virtual ICollection<History> HistoryEntries { get; set; }
    }
}