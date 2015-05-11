// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackagePipeline
    {
        IList<PackagePipelineStage> _stages;

        public PackagePipeline()
        {
            _stages = new List<PackagePipelineStage>();
        }

        public void Add(PackagePipelineStage stage)
        {
            _stages.Add(stage);
        }

        public bool Execute(PipelinePackage package, PackagePipelineContext context)
        {
            if (!package.Stream.CanSeek)
            {
                throw new ArgumentException("package stream must be seekable");
            }

            bool f = true;

            foreach (PackagePipelineStage stage in _stages)
            {
                package.Stream.Seek(0, SeekOrigin.Begin);

                f = stage.Execute(package, context);
                if (!f)
                {
                    break;
                }
            }

            context.MergeStageResults();
            return f;
        }
    }
}
