// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PipelinePackage
    {
        public PipelinePackage()
        {
        }

        public PipelinePackage(Stream stream)
            : this(stream, DateTime.UtcNow)
        {
        }

        public PipelinePackage(Stream stream, DateTime published, string owner = null)
        {
            Stream = stream;
            Published = published;
            Owner = owner;
        }

        public Stream Stream { get; set; }
        public DateTime Published { get; set; }
        public string Owner { get; set; }
    }
}
