// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public sealed class BackfillTfmMetadataCommand : BackfillCommand<List<string>>
    {
        protected override string MetadataFileName => "tfmMetadata.txt";
        
        protected override MetadataSourceType SourceType => MetadataSourceType.Nupkg;
        
        protected override string QueryIncludes => $"{nameof(Package.SupportedFrameworks)}";

        protected override bool UpdateNeedsContext => true;

        public static void Configure(CommandLineApplication config)
        {
            Configure<BackfillTfmMetadataCommand>(config);
        }

        protected override List<string> ReadMetadata(IList<string> files, NuspecReader nuspecReader)
        {
            var supportedTFMs = new List<string>();
            if (_packageService == null)
            {
                return supportedTFMs;
            }

            var supportedFrameworks = _packageService.GetSupportedFrameworks(nuspecReader, files);
            foreach (var tfm in supportedFrameworks)
            {
                supportedTFMs.Add(tfm.GetShortFolderName());
            }

            return supportedTFMs;
        }

        protected override bool ShouldWriteMetadata(List<string> metadata) => true;

        protected override void ConfigureClassMap(PackageMetadataClassMap map)
        {
            map.Map(x => x.Metadata).Index(3);
        }

        protected override void UpdatePackage(EntitiesContext context, Package package, List<string> metadata)
        {
            var existingTFMs = package.SupportedFrameworks == null
                ? Enumerable.Empty<string>()
                : package.SupportedFrameworks.Select(f => f.FrameworkName.GetShortFolderName()).OrderBy(f => f);

            var newTFMs = metadata == null || metadata.Count == 0
                ? Enumerable.Empty<string>() 
                : metadata.OrderBy(f => f);

            if (Enumerable.SequenceEqual(existingTFMs, newTFMs))
            {
                return; // nothing to change
            }

            // clean out the old (which will be left unattached in table otherwise) before adding new
            if (package.SupportedFrameworks != null)
            {
                foreach (var supportedFramework in package.SupportedFrameworks.ToList())
                {
                    package.SupportedFrameworks.Remove(supportedFramework);
                    context.PackageFrameworks.Remove(supportedFramework);
                }
            }

            package.SupportedFrameworks = newTFMs.Select(f => new PackageFramework {Package = package, TargetFramework = f}).ToList();
        }
    }
}
