// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig;

namespace NuGetGallery.Infrastructure.Mail
{
    /// <summary>
    /// Abstract base class for building email messages relying on Markdown syntax.
    /// </summary>
    public abstract class MarkdownEmailBuilder : EmailBuilder
    {
        protected override string GetPlainTextBody()
        {
            return Markdown.ToPlainText(GetMarkdownBody());
        }
    }
}
