// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// Represents a package ID reservation conflict, but all of the namespaces that prevented the upload have no
    /// owners.
    /// </summary>
    public class OwnerlessNamespaceIdConflictMessage : IValidationMessage
    {
        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage => Strings.UploadPackage_OwnerlessIdNamespaceConflict;
        public string RawHtmlMessage => Strings.UploadPackage_OwnerlessIdNamespaceConflictHtml;
    }
}