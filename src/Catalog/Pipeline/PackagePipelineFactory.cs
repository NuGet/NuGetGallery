// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Pipeline
{
    public class PackagePipelineFactory
    {
        public static PackagePipeline Create()
        {
            PackagePipeline pipeline = new PackagePipeline();

            pipeline.Add(new NuspecStage());
            pipeline.Add(new PackageHashStage());
            pipeline.Add(new PackageEntriesStage());
            pipeline.Add(new PublicationDetailsStage());
            pipeline.Add(new CommitDetailsStage());
            //  etc

            return pipeline;
        }
    }
}
