// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class IconUrlDeprecationValidationMessage : IValidationMessage
    {
        public string PlainTextMessage => $"{Strings.UploadPackage_IconUrlDeprecated} https://aka.ms/deprecateIconUrl";

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage => $"{Strings.UploadPackage_IconUrlDeprecated.Replace("<", "&lt;").Replace(">", "&gt;")} <a href=\"https://aka.ms/deprecateIconUrl\">{Strings.UploadPackage_LearnMore}</a>.";
    }
}