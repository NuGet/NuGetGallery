// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GalleryTools.Commands
{
    public sealed class BackfillTfmMetadataCommand : BackfillCommand<List<string>>
    {
        protected override string MetadataFileName => "tfmMetadata.txt";
        
        protected override MetadataSourceType SourceType => MetadataSourceType.Nupkg;
        
        protected override Expression<Func<Package, object>> QueryIncludes => p => p.SupportedFrameworks;

        protected override int CollectBatchSize => 1000;

        public static void Configure(CommandLineApplication config)
        {
            Configure<BackfillTfmMetadataCommand>(config);
        }

        protected override List<string> ReadMetadata(IList<string> files, NuspecReader nuspecReader)
        {
            var supportedTFMs = new List<string>();

            // We wrap this int a try catch because fetching supported frameworks on existing packages can sometimes throw
            // ArgumentExceptions due to format errors (usually portable TFM formatting). In this case we'll clear the TFMs.
            var supportedFrameworks = Enumerable.Empty<NuGetFramework>();
            try
            {
                supportedFrameworks = _packageService.GetSupportedFrameworks(nuspecReader, files);
                foreach (var tfm in supportedFrameworks)
                {
                    // We wrap this in a try-catch because some poorly-crafted portable TFMs will make it through GetSupportedFrameworks and fail here, e.g. for a
                    // non-existent profile name like "Profile1", which will cause GetShortFolderName to throw. We want to fail silently for these as this is a known
                    // scenario (more useful error log) and not failing will allow us to  still capture all of the valid TFMs.
                    // See https://github.com/NuGet/NuGet.Client/blob/ba008e14611f4fa518c2d02ed78dfe5969e4a003/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L297
                    // See https://github.com/NuGet/NuGet.Client/blob/ba008e14611f4fa518c2d02ed78dfe5969e4a003/src/NuGet.Core/NuGet.Frameworks/FrameworkNameProvider.cs#L487                }
                    try
                    {
                        var tfmToAdd = tfm.ToShortNameOrNull();
                        if (!string.IsNullOrEmpty(tfmToAdd))
                        {
                            supportedTFMs.Add(tfmToAdd);
                        }
                    }
                    catch
                    {
                        // skip this TFM and only collect well-formatted ones
                    }
                }
            }
            catch (ArgumentException)
            {
                // do nothing--this is a known scenario and we'll skip this package quietly, which will give us a more useful error log file
            }

            return supportedTFMs;
        }

        protected override bool ShouldWriteMetadata(List<string> metadata) => true;

        protected override void ConfigureClassMap(PackageMetadataClassMap map)
        {
            map.Map(x => x.Metadata).Index(3);
        }

        protected override void UpdatePackage(Package package, List<string> metadata, EntitiesContext context)
        {
            // Note that extracting old TFMs may throw for formatting reasons. In this case we'll force a full replacement by leaving the collection empty.
            var existingTFMs = Enumerable.Empty<string>();
            try
            {
                if (package.SupportedFrameworks != null)
                {
                    // We'll force this enumerable to a list to force all potential throws
                    existingTFMs = package.SupportedFrameworks.Select(f => f.FrameworkName.GetShortFolderName()).OrderBy(f => f).ToList();
                }
            }
            catch
            {
                // do nothing and replace in full
            }

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
