# Progress: 01-storage-foundation

## STATUS

COMPLETED

## Implementation

- Added `DownloadFileResult`/`DownloadFileResultType` to Gallery.Core for redirect, local-file, and not-found outcomes.
- Moved availability and neutral download contracts onto `ICoreFileStorageService`; removed `IFileStorageService`.
- Moved `FileSystemFileStorageService`, `IFileSystemService`, `FileSystemService`, and `LocalFileReference` into Gallery.Core.
- Added the legacy web path resolver and adapter so `HostingEnvironment.MapPath` remains at the System.Web boundary and the original availability check semantics are retained.
- Converted Blob/local implementations, all direct consumers, Autofac keyed registrations, Microsoft DI registration, mocks, and registration discovery tests to `ICoreFileStorageService`.
- Propagated neutral results through package and symbol-package services and translated them to the same MVC `RedirectResult`, `FilePathResult`, or `HttpNotFoundResult` only in `ApiController`.
- Added coverage for all three MVC mappings and Blob/filesystem availability.

## Validation

- `dotnet build src\NuGetGallery.Core\NuGetGallery.Core.csproj --no-incremental`: passed for `net472` and `netstandard2.1`.
- `dotnet build src\NuGetGallery.Services\NuGetGallery.Services.csproj --no-incremental`: passed for `net472` and `netstandard2.1`.
- `NuGetGallery.sln` with Visual Studio MSBuild 18, Debug: passed.
- `NuGet.Server.Common.sln` with Visual Studio MSBuild 18, Debug: passed.
- `NuGet.Jobs.sln` with Visual Studio MSBuild 18, Debug: passed from a temporary mapped drive pointing at this worktree. The normal long worktree path hit Windows `MAX_PATH` in the pre-existing `Validation.PackageSigning.RevalidateCertificate.Tests` intermediate path; the mapping was removed after the build.
- `src\AccountDeleter\AccountDeleter.csproj` with Visual Studio MSBuild 18, Debug: passed.
- Targeted `NuGetGallery.Facts` storage/package/controller/registration tests: 634 passed, 0 failed, 0 skipped.
- Targeted `NuGetGallery.Core.Facts` Blob storage tests: 205 passed, 0 failed, 0 skipped.
- `git diff --check`: passed.
- Final searches under `src` and `tests`: no remaining `IFileStorageService` or MVC-returning download API references.

## Modified files

- `.github/upgrades/scenarios/dotnet-version-upgrade/scenario-instructions.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-storage-foundation/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/01-storage-foundation/progress-details.md`
- `src/AccountDeleter/Job.cs`
- `src/NuGetGallery.Core/Services/CloudBlobCoreFileStorageService.cs`
- `src/NuGetGallery.Core/Services/DownloadFileResult.cs` (new)
- `src/NuGetGallery.Core/Services/FileSystemFileStorageService.cs` (moved from legacy web project)
- `src/NuGetGallery.Core/Services/FileSystemService.cs` (moved from legacy web project)
- `src/NuGetGallery.Core/Services/ICoreFileStorageService.cs`
- `src/NuGetGallery.Core/Services/IFileReference.cs`
- `src/NuGetGallery.Core/Services/IFileSystemService.cs` (moved from legacy web project)
- `src/NuGetGallery.Core/Services/LocalFileReference.cs` (moved from legacy web project)
- `src/NuGetGallery.Services/Storage/CloudBlobFileStorageService.cs`
- `src/NuGetGallery.Services/Storage/ContentService.cs`
- `src/NuGetGallery.Services/Storage/IFileStorageService.cs` (deleted)
- `src/NuGetGallery/App_Start/DefaultDependenciesModule.cs`
- `src/NuGetGallery/App_Start/StorageDependent.cs`
- `src/NuGetGallery/Controllers/ApiController.cs`
- `src/NuGetGallery/NuGetGallery.csproj`
- `src/NuGetGallery/Services/CertificateService.cs`
- `src/NuGetGallery/Services/FileStoragePathResolver.cs` (new)
- `src/NuGetGallery/Services/FileSystemFileStorageService.cs` (moved to Gallery.Core)
- `src/NuGetGallery/Services/FileSystemService.cs` (moved to Gallery.Core)
- `src/NuGetGallery/Services/IFileSystemService.cs` (moved to Gallery.Core)
- `src/NuGetGallery/Services/IPackageFileService.cs`
- `src/NuGetGallery/Services/ISymbolPackageFileService.cs`
- `src/NuGetGallery/Services/LegacyFileSystemFileStorageService.cs` (new)
- `src/NuGetGallery/Services/LocalFileReference.cs` (moved to Gallery.Core)
- `src/NuGetGallery/Services/PackageFileService.cs`
- `src/NuGetGallery/Services/StatusService.cs`
- `src/NuGetGallery/Services/SymbolPackageFileService.cs`
- `src/NuGetGallery/Services/UploadFileService.cs`
- `tests/NuGetGallery.Core.Facts/Services/CloudBlobCoreFileStorageServiceFacts.cs`
- `tests/NuGetGallery.Facts/App_Start/StorageDependentFacts.cs`
- `tests/NuGetGallery.Facts/Controllers/ApiControllerFacts.cs`
- `tests/NuGetGallery.Facts/Services/CertificateServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/CloudBlobFileStorageServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/ContentServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/FileSystemFileStorageServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/PackageFileServiceFacts.cs`
- `tests/NuGetGallery.Facts/Services/UploadFileServiceFacts.cs`
