using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("deletepackagefile", "Deletes a specific package file", AltName = "dpf")]
    public class DeletePackageFileTask : PackageVersionTask
    {
        [Option("Storage account in which to place audit records and backups, usually provided by the environment")]
        public CloudStorageAccount BackupStorage { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(PackageId, "PackageId");
            ArgCheck.Required(PackageVersion, "PackageVersion");
            ArgCheck.Required(PackageHash, "PackageHash");

            if (BackupStorage == null && CurrentEnvironment != null)
            {
                BackupStorage = CurrentEnvironment.BackupStorage;
            }
            ArgCheck.RequiredOrConfig(BackupStorage, "BackupStorage");
        }

        public override void ExecuteCommand()
        {
            new BackupPackageFileTask
                {
                    BackupStorage = BackupStorage,
                    StorageAccount = StorageAccount,
                    PackageId = PackageId,
                    PackageVersion = PackageVersion,
                    PackageHash = PackageHash,
                    WhatIf = WhatIf
                }.ExecuteCommand();

            var packageFiles = GetPackageFileService();
            var fileName = FileConventions.GetPackageFileName(PackageId, PackageVersion);
            if (packageFiles.PackageFileExists(PackageId, PackageVersion))
            {
                Log.Info("Deleting package file '{0}'.", fileName);
                if (!WhatIf)
                {
                    packageFiles.DeletePackageFileAsync(PackageId, PackageVersion).Wait();
                }
            }
            else
            {
                Log.Warn("Package file does not exist '{0}'.", fileName);
            }
        }
    }
}
