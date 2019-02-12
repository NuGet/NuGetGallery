// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery.Messages
{
    public class GalleryMessageWithLinks : IGalleryMessage
    {
        private string _linkFormat = $"<a src=\"{1}\" target=\"_blank\">{0}</a>";
        private string _baseMessage;
        private string[] _plainObjectArguments;
        private string[] _linkObjectArguments;

        public GalleryMessageWithLinks(string message, LinkMessageFormat[] args)
        {
            _baseMessage = message ?? throw new ArgumentNullException(nameof(message));

            _plainObjectArguments = args
                .Select(x => x.Message)
                .ToArray();

            _linkObjectArguments = args
                .Select(x => {
                    return x.IsUrlMessage
                        ? string.Format(_linkFormat, x.Message, x.Link)
                        : x.Message;
                })
                .ToArray();
        }

        public string PlainTextMessage => string.Format(_baseMessage, _plainObjectArguments);

        public bool HasRawHtmlRepresentation => true;

        public string RawHtmlMessage => string.Format(_baseMessage, _linkObjectArguments);

        public override string ToString()
        {
            return RawHtmlMessage;
        }
    }
}