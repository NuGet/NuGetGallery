// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public sealed class BackfillDevelopmentDependencyCommand : BackfillCommand<bool>
    {
        protected override string MetadataFileName => "developmentDependencyMetadata.txt";

        public static void Configure(CommandLineApplication config)
        {
            Configure<BackfillDevelopmentDependencyCommand>(config);
        }

        protected override bool ReadMetadata(NuspecReader reader)
        {
            return reader.GetDevelopmentDependency();
        }

        protected override bool ShouldWriteMetadata(bool metadata)
        {
            // There's no point in updating packages where developmentDependency is false.
            return metadata;
        }

        protected override void ConfigureClassMap(PackageMetadataClassMap map)
        {
            map.Map(x => x.Metadata).Index(3);
        }

        protected override void UpdatePackage(Package package, bool metadata, EntitiesContext context)
        {
            package.DevelopmentDependency = metadata;
        }
    }
}
