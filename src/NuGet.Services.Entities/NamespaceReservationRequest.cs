// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    public class NamespaceReservationRequest : IEntity
    {
        [Key]
        public int Key { get; set; }

        public int SubmittedByUserKey { get; set; }
        public virtual User SubmittedByUser { get; set; }

        [Required]
        [StringLength(128)]
        public string Namespace { get; set; }

        [Required]
        public string RequestedOwnersJson { get; set; }

        [Required]
        [StringLength(4000)]
        public string Justification { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedTimestamp { get; set; }

        // This is the assessment decision, not the result of creating a reservation.
        [Required]
        [StringLength(16)]
        public string Status { get; set; }

        [StringLength(4000)]
        public string Reason { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? CompletedTimestamp { get; set; }
    }
}