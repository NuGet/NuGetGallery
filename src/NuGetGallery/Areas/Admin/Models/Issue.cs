// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Issue : IEntity
    {
        [Key]
        public int Key { get; set; }

        [Required(ErrorMessage = "Please enter your username")]
        [StringLength(50)]
        public string CreatedBy { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CreatedDate { get; set; }

        [Required(ErrorMessage = "Please enter an issue title")]
        [StringLength(1000)]
        public string IssueTitle { get; set; }

        public int? AssignedTo { get; set; }

        public int? IssueStatus { get; set; }

        [Required(ErrorMessage = "Please enter your the details of the issue")]
        public string Details { get; set; }

        public string Comments { get; set; }

        [Required(ErrorMessage = "Please enter the site root")]
        [DataType(DataType.Url)]
        public string SiteRoot { get; set; }

        [Required(ErrorMessage = "Please enter the package Id")]
        [StringLength(300)]
        public string PackageID { get; set; }

        [Required(ErrorMessage = "Please enter the package version")]
        [StringLength(300)]
        public string PackageVersion { get; set; }

        [Required(ErrorMessage = "Please enter the owner email address")]
        [StringLength(100)]
        [DataType(DataType.EmailAddress)]
        public string OwnerEmail { get; set; }

        [StringLength(100)]
        public string Reason { get; set; }
    }
}