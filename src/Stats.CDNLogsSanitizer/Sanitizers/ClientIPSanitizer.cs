// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Auditing;
using Stats.AzureCdnLogs.Common;

namespace Stats.CDNLogsSanitizer
{
    public class ClientIPSanitizer : ISanitizer
    {
        private const string _headerValue = "c-ip";
        private readonly int _headerValueIndex;
        private readonly LogHeaderMetadata _headerMetadata;

        public ClientIPSanitizer(LogHeaderMetadata headerMetadata)
        {
            _headerMetadata = headerMetadata ?? throw new ArgumentNullException(nameof(headerMetadata));
            _headerValueIndex = headerMetadata.GetIndex(_headerValue) ?? throw new ArgumentException(nameof(headerMetadata.Header));
        }

        public void SanitizeLogLine(ref string line)
        {
            var lineSegments = ExtensionsUtils.GetSegmentsFromCSV(line, _headerMetadata.Delimiter);
            string clientIp = lineSegments[_headerValueIndex];
            lineSegments[_headerValueIndex] = Obfuscator.ObfuscateIp(clientIp);

            line = string.Join(new string(_headerMetadata.Delimiter, 1), lineSegments);
        }  
    }
}
