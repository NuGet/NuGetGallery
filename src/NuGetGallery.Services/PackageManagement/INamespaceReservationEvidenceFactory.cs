// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public interface INamespaceReservationEvidenceFactory
    {
        /// <summary>
        /// Creates local, read-only evidence scoped to trusted server-side request values.
        /// The namespace is a package-ID base (no trailing dot), at most 127 characters.
        /// Between one and nineteen distinct, positive owner keys are accepted.
        /// </summary>
        INamespaceReservationEvidenceSession Create(int submitterKey, int[] ownerKeys, string namespaceValue);
    }
}