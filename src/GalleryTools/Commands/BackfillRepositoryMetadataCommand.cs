// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;

namespace GalleryTools.Commands
{
    public sealed class BackfillRepositoryMetadataCommand : BackfillCommand<RepositoryMetadata>
    {
        protected override string MetadataFileName => "repositoryMetadata.txt";

        public static void Configure(CommandLineApplication config)
        {
            Configure<BackfillRepositoryMetadataCommand>(config);
        }

        protected override RepositoryMetadata ReadMetadata(NuspecReader reader)
        {
            return reader.GetRepositoryMetadata();
        }

        protected override bool ShouldWriteMetadata(RepositoryMetadata metadata)
        {
            return !string.IsNullOrEmpty(metadata.Branch)
                || !string.IsNullOrEmpty(metadata.Commit)
                || !string.IsNullOrEmpty(metadata.Type)
                || !string.IsNullOrEmpty(metadata.Url);
        }

        protected override void ConfigureClassMap(PackageMetadataClassMap map)
        {
            map.Map(x => x.Metadata.Type).Index(3);
            map.Map(x => x.Metadata.Url).Index(4);
            map.Map(x => x.Metadata.Branch).Index(5);
            map.Map(x => x.Metadata.Commit).Index(6);
        }

        protected override void UpdatePackage(Package package, RepositoryMetadata metadata)
        {
            package.RepositoryUrl = metadata.Url;

            if (metadata.Type.Length >= 100)
            {
                // TODO: Log error.
            }
            else
            {
                package.RepositoryType = metadata.Type;
            }
        }
    }
}
