// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class VulnerableDirectDependencyMessage : IValidationMessage
    {
        public VulnerableDirectDependencyMessage(string directDependencyMessage)
        {
            PlainTextMessage = directDependencyMessage;
        }

        public bool HasRawHtmlRepresentation => true;
        public string PlainTextMessage { get; }
        public string RawHtmlMessage => PlainTextMessage;
    }
}