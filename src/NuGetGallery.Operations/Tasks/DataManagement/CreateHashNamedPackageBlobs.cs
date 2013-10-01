using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.DataManagement
{
    [Command("createhashnamedpackageblobs", "Creates Hash-Named Package Blobs (one-off data management)", AltName = "chnpb", IsSpecialPurpose = true)]
    public class CreateHashNamedPackageBlobs : DatabaseAndStorageTask
    {
        public override void ExecuteCommand()
        {
            // Step 1:
            // For each package [version] in the packages table, create the hash named nupkg blob for it.
            EntitiesContext ctx = new EntitiesContext(ConnectionString.ConnectionString, readOnly: false);
            var allPackages = ctx.Set<NuGetGallery.Package>()
                .AsQueryable().Include(p => p.PackageRegistration);

            var packageFileService = GetPackageFileService();
            var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
            Parallel.ForEach(
                allPackages, 
                options, 
                (package) =>
                {
                    if (!packageFileService.PackageFileExists(package.PackageRegistration.Id, package.GetNormalizedVersion(), package.Hash))
                    {
                        Log.Info("Copying - creating hashed package for {0} {1} {2}",
                            package.PackageRegistration.Id, package.GetNormalizedVersion(), package.Hash);

                        if (!WhatIf)
                        {
                            packageFileService.BeginCopyPackageFileToHashedAsync(
                                package.PackageRegistration.Id, package.GetNormalizedVersion(), package.Hash)
                                .Wait();

                            packageFileService.EndCopyPackageFileToHashedAsync(
                                package.PackageRegistration.Id, package.GetNormalizedVersion(), package.Hash)
                                .Wait();
                        }
                    }
                });

            throw new NotImplementedException();

            // Step 2:
            // For each package history in the packagehistories table, create the hash named nupkg blob for it.

            // Done! (assumes Gallery+Worker already deployed, in any order)
            // Gallery code is already creating hash named nupkgs for newly uploaded packages
            // Worker code is already creating hash named nupkgs whenever it process package edits
        }
    }
}
