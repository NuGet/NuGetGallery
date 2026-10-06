// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Only the three customer-supplied form fields are sent to the model.
    /// No database keys, resolved account metadata, lookup results, or database access are provided.
    /// </summary>
    public class NamespaceReservationAssessmentInput
    {
        public string Namespace { get; set; }
        public string Owner { get; set; }
        public string Justification { get; set; }
    }
}