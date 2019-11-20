﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Globalization;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin.Models
{
    public class LuceneInfoModel
    {
        public DateTime? LastUpdated { get; set; }
        public string Directory { get; set; }
        public int DocumentCount { get; set; }
        public long IndexSize { get; set; }
        public bool IsLocal { get; set; }
        public LuceneIndexLocation Location { get; set; }

        public string FormatIndexSize()
        {
            // Less than a KB?
            if (IndexSize < 1024)
            {
                return IndexSize.ToString("0", CultureInfo.CurrentCulture) + "b";
            }
            // Less than an MB?
            else if (IndexSize < (1024 * 1024))
            {
                return (IndexSize / (double)1024).ToString("0.00", CultureInfo.CurrentCulture) + "KB";
            }
            // Less than a GB?
            else if (IndexSize < (1024 * 1024 * 1024))
            {
                return (IndexSize / (double)(1024 * 1024)).ToString("0.00", CultureInfo.CurrentCulture) + "MB";
            }
            return (IndexSize / (double)(1024 * 1024 * 1024)).ToString("0.00", CultureInfo.CurrentCulture) + "GB";
        }
    }
}
