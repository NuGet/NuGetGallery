// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.Models
{
    [Table("History")]
    public partial class History : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Key { get; set; }

        public DateTime EntryDate { get; set; }
        public string EditedBy { get; set; }
        public string Comments { get; set; }

        public int IssueId { get; set; }
        public int IssueStatusId { get; set; }
        public int? AssignedToId { get; set; }
    }
}