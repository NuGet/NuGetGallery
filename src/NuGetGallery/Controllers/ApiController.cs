// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Auditing.AuditedEntities;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Packaging;
using PackageIdValidator = NuGetGallery.Packaging.PackageIdValidator;

namespace NuGetGallery
{
    public partial class ApiController
        : AppController
    {
        public IEntitiesContext EntitiesContext { get; set; }
        public INuGetExeDownloaderService NugetExeDownloaderService { get; set; }
        public IPackageFileService PackageFileService { get; set; }
        public IPackageService PackageService { get; set; }
        public IUserService UserService { get; set; }
        public IStatisticsService StatisticsService { get; set; }
        public IContentService ContentService { get; set; }
        public ISearchService SearchService { get; set; }
        public IIndexingService IndexingService { get; set; }
        public IAutomaticallyCuratePackageCommand AutoCuratePackage { get; set; }
        public IStatusService StatusService { get; set; }
        public IMessageService MessageService { get; set; }
        public AuditingService AuditingService { get; set; }
        public IGalleryConfigurationService ConfigurationService { get; set; }

        protected ApiController()
        {
            AuditingService = AuditingService.None;
        }

        public ApiController(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IContentService contentService,
            IIndexingService indexingService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratePackage,
            IStatusService statusService,
            IMessageService messageService,
            AuditingService auditingService,
            IGalleryConfigurationService configurationService)
        {
            EntitiesContext = entitiesContext;
            PackageService = packageService;
            PackageFileService = packageFileService;
            UserService = userService;
            NugetExeDownloaderService = nugetExeDownloaderService;
            ContentService = contentService;
            StatisticsService = null;
            IndexingService = indexingService;
            SearchService = searchService;
            AutoCuratePackage = autoCuratePackage;
            StatusService = statusService;
            MessageService = messageService;
            AuditingService = auditingService;
            ConfigurationService = configurationService;
        }

        public ApiController(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService,
            IUserService userService,
            INuGetExeDownloaderService nugetExeDownloaderService,
            IContentService contentService,
            IIndexingService indexingService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratePackage,
            IStatusService statusService,
            IStatisticsService statisticsService,
            IMessageService messageService,
            AuditingService auditingService,
            IGalleryConfigurationService configurationService)
            : this(entitiesContext, packageService, packageFileService, userService, nugetExeDownloaderService, contentService, indexingService, searchService, autoCuratePackage, statusService, messageService, auditingService, configurationService)
        {
            StatisticsService = statisticsService;
        }

        [HttpGet]
        [ActionName("GetPackageApi")]
        public virtual async Task<ActionResult> GetPackage(string id, string version)
        {
            // some security paranoia about URL hacking somehow creating e.g. open redirects
            // validate user input: explicit calls to the same validators used during Package Registrations
            // Ideally shouldn't be necessary?
            if (!PackageIdValidator.IsValidPackageId(id ?? ""))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The format of the package id is invalid");
            }

            // if version is non-null, check if it's semantically correct and normalize it.
            if (!String.IsNullOrEmpty(version))
            {
                NuGetVersion dummy;
                if (!NuGetVersion.TryParse(version, out dummy))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The package version is not a valid semantic version");
                }

                // Normalize the version
                version = NuGetVersionNormalizer.Normalize(version);
            }
            else
            {
                // if version is null, get the latest version from the database.
                // This ensures that on package restore scenario where version will be non null, we don't hit the database.
                try
                {
                    var package = PackageService.FindPackageByIdAndVersion(id, version, allowPrerelease: false);
                    if (package == null)
                    {
                       return new HttpStatusCodeWithBodyResult(HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                    }
                    version = package.NormalizedVersion;

                }
                catch (SqlException e)
                {
                    QuietLog.LogHandledException(e);

                    // Database was unavailable and we don't have a version, return a 503
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
                }
                catch (DataException e)
                {
                    QuietLog.LogHandledException(e);

			        // Database was unavailable and we don't have a version, return a 503
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
                }
            }

            if (ConfigurationService.Features.TrackPackageDownloadCountInLocalDatabase)
            {
                await PackageService.IncrementDownloadCountAsync(id, version);
            }

            return await PackageFileService.CreateDownloadPackageActionResultAsync(
                HttpContext.Request.Url,
                id, version);
        }

        [HttpGet]
        [ActionName("GetNuGetExeApi")]
        [RequireHttps]
        [OutputCache(VaryByParam = "none", Location = OutputCacheLocation.ServerAndClient, Duration = 600)]
        public virtual Task<ActionResult> GetNuGetExe()
        {
            return NugetExeDownloaderService.CreateNuGetExeDownloadActionResultAsync(HttpContext.Request.Url);
        }

        [HttpGet]
        [ActionName("StatusApi")]
        public async virtual Task<ActionResult> Status()
        {
            if (StatusService == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Status service is unavailable");
            }
            return await StatusService.GetStatus();
        }

        [HttpGet]
        [RequireSsl]
        [ApiAuthorize]
        [ActionName("VerifyPackageKey")]
        public virtual ActionResult VerifyPackageKey(string id, string version)
        {
            if (!String.IsNullOrEmpty(id))
            {
                // If the partialId is present, then verify that the user has permission to push for the specific Id \ version combination.
                var package = PackageService.FindPackageByIdAndVersion(id, version);
                if (package == null)
                {
                    return new HttpStatusCodeWithBodyResult(
                        HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
                }

                var user = GetCurrentUser();
                if (!package.IsOwner(user))
                {
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                }
            }

            return new EmptyResult();
        }

        [HttpPut]
        [RequireSsl]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.All, NuGetScopes.PackagePushNew, NuGetScopes.PackagePush)]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePut()
        {
            return CreatePackageInternal();
        }

        [HttpPost]
        [RequireSsl]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.All, NuGetScopes.PackagePushNew, NuGetScopes.PackagePush)]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePost()
        {
            return CreatePackageInternal();
        }

        private async Task<ActionResult> CreatePackageInternal()
        {
            // Get the user
            var user = GetCurrentUser();

            using (var packageStream = ReadPackageFromRequest())
            {
                try
                {
                    using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var reference = DateTime.UtcNow.AddDays(1); // allow "some" clock skew

                        var entryInTheFuture = archive.Entries.FirstOrDefault(
                            e => e.LastWriteTime.UtcDateTime > reference);

                        if (entryInTheFuture != null)
                        {
                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, string.Format(
                               CultureInfo.CurrentCulture,
                               Strings.PackageEntryFromTheFuture,
                               entryInTheFuture.Name));
                        }
                    }

                    using (var packageToPush = new PackageArchiveReader(packageStream, leaveStreamOpen: false))
                    {
                        NuspecReader nuspec = null;
                        try
                        {
                            nuspec = packageToPush.GetNuspecReader();
                        }
                        catch (Exception ex)
                        {
                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UploadPackage_InvalidNuspec,
                                ex.Message));
                        }

                        if (nuspec.GetMinClientVersion() > Constants.MaxSupportedMinClientVersion)
                        {
                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UploadPackage_MinClientVersionOutOfRange,
                                nuspec.GetMinClientVersion()));
                        }

                        // Ensure that the user can push packages for this partialId.
                        var packageRegistration = PackageService.FindPackageRegistrationById(nuspec.GetId());
                        if (packageRegistration == null)
                        {
                            // Check if API key allows pushing a new package id
                            var identity = User.Identity as ClaimsIdentity;
                            if (!identity.HasScope(NuGetScopes.All, NuGetScopes.PackagePushNew))
                            {
                                // User cannot push a new package ID as the API key scope does not allow it
                                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Conflict,
                                    String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable,
                                        nuspec.GetId()));
                            }
                        }
                        else
                        {
                            // Is the user allowed to push this Id?
                            if (!packageRegistration.IsOwner(user))
                            {
                                // Audit that a non-owner tried to push the package
                                await AuditingService.SaveAuditRecord(
                                    new FailedAuthenticatedOperationAuditRecord(
                                        user.Username, 
                                        AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner, 
                                        attemptedPackage: new AuditedPackageIdentifier(
                                            nuspec.GetId(), nuspec.GetVersion().ToNormalizedStringSafe())));

                                // User cannot push a package to an ID owned by another user.
                                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Conflict,
                                    String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable,
                                        nuspec.GetId()));
                            }

                            // Check if a particular Id-Version combination already exists. We eventually need to remove this check.
                            string normalizedVersion = nuspec.GetVersion().ToNormalizedString();
                            bool packageExists =
                                packageRegistration.Packages.Any(
                                    p => String.Equals(
                                        p.NormalizedVersion,
                                        normalizedVersion,
                                        StringComparison.OrdinalIgnoreCase));

                            if (packageExists)
                            {
                                return new HttpStatusCodeWithBodyResult(
                                    HttpStatusCode.Conflict,
                                    String.Format(CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified,
                                        nuspec.GetId(), nuspec.GetVersion().ToNormalizedStringSafe()));
                            }
                        }

                        var packageStreamMetadata = new PackageStreamMetadata
                        {
                            HashAlgorithm = Constants.Sha512HashAlgorithmId,
                            Hash = CryptographyService.GenerateHash(packageStream.AsSeekableStream()),
                            Size = packageStream.Length,
                        };

                        var package = await PackageService.CreatePackageAsync(
                            packageToPush, 
                            packageStreamMetadata,
                            user,
                            commitChanges: false);
                        await AutoCuratePackage.ExecuteAsync(package, packageToPush, commitChanges: false);
                        await EntitiesContext.SaveChangesAsync();

                        using (Stream uploadStream = packageStream)
                        {
                            uploadStream.Position = 0;
                            await PackageFileService.SavePackageFileAsync(package, uploadStream.AsSeekableStream());
                            IndexingService.UpdatePackage(package);
                        }

                        // Write an audit record
                        await AuditingService.SaveAuditRecord(
                            new PackageAuditRecord(package, AuditedPackageAction.Create, PackageCreatedVia.Api));

                        // Notify user of push
                        MessageService.SendPackageAddedNotice(package,
                            Url.Action("DisplayPackage", "Packages", routeValues: new { id = package.PackageRegistration.Id, version = package.Version }, protocol: Request.Url.Scheme),
                            Url.Action("ReportMyPackage", "Packages", routeValues: new { id = package.PackageRegistration.Id, version = package.Version }, protocol: Request.Url.Scheme),
                            Url.Action("Account", "Users", routeValues: null, protocol: Request.Url.Scheme));

                        return new HttpStatusCodeResult(HttpStatusCode.Created);
                    }
                }
                catch (InvalidPackageException ex)
                {
                    return BadRequestForExceptionMessage(ex);
                }
                catch (InvalidDataException ex)
                {
                    return BadRequestForExceptionMessage(ex);
                }
                catch (EntityException ex)
                {
                    return BadRequestForExceptionMessage(ex);
                }
                catch (FrameworkException ex)
                {
                    return BadRequestForExceptionMessage(ex);
                }
            }
        }

        private static ActionResult BadRequestForExceptionMessage(Exception ex)
        {
            return new HttpStatusCodeWithBodyResult(
                HttpStatusCode.BadRequest,
                string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_InvalidPackage, ex.Message));
        }

        [HttpDelete]
        [RequireSsl]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.All, NuGetScopes.PackageList)]
        [ActionName("DeletePackageApi")]
        public virtual async Task<ActionResult> DeletePackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            var user = GetCurrentUser();
            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
            }

            await PackageService.MarkPackageUnlistedAsync(package);
            IndexingService.UpdatePackage(package);
            return new EmptyResult();
        }

        [HttpPost]
        [RequireSsl]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.All, NuGetScopes.PackageList)]
        [ActionName("PublishPackageApi")]
        public virtual async Task<ActionResult> PublishPackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            User user = GetCurrentUser();
            if (!package.IsOwner(user))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, String.Format(CultureInfo.CurrentCulture, Strings.ApiKeyNotAuthorized, "publish"));
            }

            await PackageService.MarkPackageListedAsync(package);
            IndexingService.UpdatePackage(package);
            return new EmptyResult();
        }

        public virtual async Task<ActionResult> Team()
        {
            var team = await ContentService.GetContentItemAsync(Constants.ContentNames.Team, TimeSpan.FromHours(1));
            return Content(team.ToString(), "application/json");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            var exception = filterContext.Exception;
            if (exception is ReadOnlyModeException)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.ServiceUnavailable, exception.Message);
            }
            else
            {
                var request = filterContext.HttpContext.Request;
                filterContext.ExceptionHandled = true;
                filterContext.Result = new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.InternalServerError, exception.Message, request.IsLocal ? exception.StackTrace : exception.Message);
            }
        }

        protected internal virtual Stream ReadPackageFromRequest()
        {
            Stream stream;
            if (Request.Files.Count > 0)
            {
                // If we're using the newer API, the package stream is sent as a file.
                // ReSharper disable once PossibleNullReferenceException
                stream = Request.Files[0].InputStream;
            }
            else
            {
                stream = Request.InputStream;
            }

            return stream;
        }

        [HttpGet]
        [ActionName("PackageIDs")]
        public virtual async Task<ActionResult> GetPackageIds(string partialId, bool? includePrerelease)
        {
            var query = GetService<IPackageIdsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(partialId, includePrerelease)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("PackageVersions")]
        public virtual async Task<ActionResult> GetPackageVersions(string id, bool? includePrerelease)
        {
            var query = GetService<IPackageVersionsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(id, includePrerelease)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("StatisticsDownloadsApi")]
        public virtual async Task<ActionResult> GetStatsDownloads(int? count)
        {
            var result = await StatisticsService.LoadDownloadPackageVersions();

            if (result.Loaded)
            {
                int i = 0;

                JArray content = new JArray();
                foreach (StatisticsPackagesItemViewModel row in StatisticsService.DownloadPackageVersionsAll)
                {
                    JObject item = new JObject();

                    item.Add("PackageId", row.PackageId);
                    item.Add("PackageVersion", row.PackageVersion);
                    item.Add("Gallery", Url.PackageGallery(row.PackageId, row.PackageVersion));
                    item.Add("PackageTitle", row.PackageTitle ?? row.PackageId);
                    item.Add("PackageDescription", row.PackageDescription);
                    item.Add("PackageIconUrl", row.PackageIconUrl ?? Url.PackageDefaultIcon());
                    item.Add("Downloads", row.Downloads);

                    content.Add(item);

                    i++;

                    if (count.HasValue && count.Value == i)
                    {
                        break;
                    }
                }

                return new ContentResult
                {
                    Content = content.ToString(),
                    ContentType = "application/json"
                };
            }

            return new HttpStatusCodeResult(HttpStatusCode.NotFound);
        }
    }
}
