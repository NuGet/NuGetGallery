// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Stats.AzureCdnLogs.Common;

namespace Stats.CollectAzureCdnLogs
{
    public sealed class RawLogFileInfo
    {
        private const string _underscore = "_";
        private const string _logFileNameDateFormat = "yyyyMMdd";
        private const string _dot = ".";
        private const char _zero = '0';
        private const string _contentTypeGzip = "application/x-gzip";

        public RawLogFileInfo(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            ContentType = "text/plain";
            Uri = uri;

            TryParseFileName();
        }

        public AzureCdnPlatform AzureCdnPlatform { get; private set; }
        public string AzureCdnAccountNumber { get; private set; }
        public string Extension { get; private set; }
        public string FileName { get; private set; }
        public DateTime GeneratedDate { get; private set; }
        public int RollingFileNumber { get; private set; }
        public Uri Uri { get; private set; }
        public string ContentType { get; private set; }
        public bool IsPendingDownload { get; private set; }

        public override string ToString()
        {
            return FileName;
        }

        /// <summary>
        /// Filename format: {azureCdnPlatformPrefix}_{azureCdnAccountNumber}_{yyyyMMdd}_{nnnn}.log.gz
        /// See https://my.edgecast.com/uploads/ubers/1/docs/en-US/webhelp/w/CDNHelpCenter/Content/Raw_Log_Files/Raw_Log_File_Naming_Convention.htm
        /// </summary>
        private void TryParseFileName()
        {
            FileName = Uri.LocalPath.Split(new[] { Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Last();

            var fileNameParts = FileName.Split(new[] { _underscore }, StringSplitOptions.RemoveEmptyEntries);
            if (fileNameParts.Count() == 4)
            {
                AzureCdnPlatform = AzureCdnPlatformExtensions.ParseAzureCdnPlatformPrefix(fileNameParts[0]);
                AzureCdnAccountNumber = fileNameParts[1];
                GeneratedDate = TryParseGeneratedDate(fileNameParts[2]);

                // split last part to extract rolling file number and file extensions
                var lastPart = fileNameParts[3].Split(new[] { _dot }, StringSplitOptions.RemoveEmptyEntries);
                if (lastPart.Count() == 3)
                {
                    RollingFileNumber = TryParseRollingFileNumber(lastPart[0]);
                    Extension = _dot + string.Join(_dot, lastPart[1], lastPart[2]);

                    if (Extension.EndsWith(FileExtensions.Gzip, StringComparison.InvariantCultureIgnoreCase))
                    {
                        ContentType = _contentTypeGzip;

                        return;
                    }
                }
                else if (lastPart.Count() == 4)
                {
                    // found an already renamed file?
                    RollingFileNumber = TryParseRollingFileNumber(lastPart[0]);
                    Extension = _dot + string.Join(_dot, lastPart[1], lastPart[2], lastPart[3]);
                    if (Extension.EndsWith(FileExtensions.Download, StringComparison.InvariantCultureIgnoreCase))
                    {
                        IsPendingDownload = true;
                        ContentType = _contentTypeGzip;

                        return;
                    }
                }
            }

            throw new InvalidRawLogFileNameException(FileName);
        }

        private int TryParseRollingFileNumber(string rollingFileNumberString)
        {
            // trim leading zeros
            var trimmedRollingFileNumberString = rollingFileNumberString.TrimStart(_zero);

            int result;
            if (int.TryParse(trimmedRollingFileNumberString, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            throw new InvalidRawLogFileNameException(FileName);
        }

        private DateTime TryParseGeneratedDate(string datePart)
        {
            DateTime result;
            if (DateTime.TryParseExact(datePart, _logFileNameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            {
                return result;
            }
            throw new InvalidRawLogFileNameException(FileName);
        }
    }
}