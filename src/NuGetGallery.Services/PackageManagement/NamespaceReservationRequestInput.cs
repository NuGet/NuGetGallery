// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetGallery
{
    // Bind only customer input, never the submitter, status, timestamps, or decision.
    public class NamespaceReservationRequestInput
    {
        public const int MaxNamespaceLength = 127;
        public const int MaxOwnerLength = 1000;
        public const int MaxJustificationLength = 4000;

        [AllowHtml]
        [Required(ErrorMessage = "Enter a namespace.")]
        [StringLength(MaxNamespaceLength, ErrorMessage = "Namespace must be 127 characters or fewer, leaving room for the dotted prefix.")]
        public string Namespace { get; set; }

        [AllowHtml]
        [Required(ErrorMessage = "Enter at least one owner.")]
        [StringLength(MaxOwnerLength, ErrorMessage = "Owner must be 1000 characters or fewer.")]
        public string Owner { get; set; }

        [AllowHtml]
        [Required(ErrorMessage = "Enter a justification.")]
        [StringLength(MaxJustificationLength, ErrorMessage = "Justification must be 4000 characters or fewer.")]
        public string Justification { get; set; }
    }
}