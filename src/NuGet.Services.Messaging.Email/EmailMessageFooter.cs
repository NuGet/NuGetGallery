// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Messaging.Email
{
    /// <summary>
    /// Utility class to construct common email message footers.
    /// </summary>
    public static class EmailMessageFooter
    {
        public static string ForPackageOwnerNotifications(EmailFormat format, string galleryOwnerDisplayName, string emailSettingsUrl)
        {
            return ForReason(
                format,
                galleryOwnerDisplayName,
                emailSettingsUrl,
                "To stop receiving emails as an owner of this package");
        }

        public static string ForContactOwnerNotifications(EmailFormat format, string galleryOwnerDisplayName, string emailSettingsUrl)
        {
            return ForReason(
                format,
                galleryOwnerDisplayName,
                emailSettingsUrl,
                "To stop receiving contact emails as an owner of this package");
        }

        private static string ForReason(EmailFormat format, string galleryOwnerDisplayName, string emailSettingsUrl, string reason)
        {
            switch (format)
            {
                case EmailFormat.PlainText:
                    // Hyperlinks within an HTML emphasis tag are not supported by the Plain Text renderer in Markdig.
                    return $@"

-----------------------------------------------
    {reason}, sign in to the {galleryOwnerDisplayName} and
    change your email notification settings ({emailSettingsUrl}).";
                case EmailFormat.Html:
                    // Hyperlinks within an HTML emphasis tag are not supported by the HTML renderer in Markdig.
                    return $@"<hr />
<em style=""font-size: 0.8em;"">
    {reason}, sign in to the {galleryOwnerDisplayName} and
    <a href=""{emailSettingsUrl}"">change your email notification settings</a>.
</em>";
                case EmailFormat.Markdown:
                    return $@"

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    {reason}, sign in to the {galleryOwnerDisplayName} and
    [change your email notification settings]({emailSettingsUrl}).
</em>";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }
    }
}