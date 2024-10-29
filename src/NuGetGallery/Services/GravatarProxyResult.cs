// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGetGallery
{
    /// <summary>
    /// The result of <see cref="IGravatarProxyService.GetAvatarOrNullAsync(string, int)"/>.
    /// </summary>
    public class GravatarProxyResult
    {
        public GravatarProxyResult(Stream avatarStream, string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException("Content type cannot be empty", nameof(contentType));
            }

            AvatarStream = avatarStream ?? throw new ArgumentNullException(nameof(avatarStream));
            ContentType = contentType;
        }

        /// <summary>
        /// The proxied Gravatar content. This stream's consumer must dispose this value.
        /// </summary>
        public Stream AvatarStream { get; }

        /// <summary>
        /// The proxied Gravatar's content type.
        /// </summary>
        public string ContentType { get; }
    }
}
