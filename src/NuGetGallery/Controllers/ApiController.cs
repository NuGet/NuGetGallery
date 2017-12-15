// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
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
        public IAuditingService AuditingService { get; set; }
        public IGalleryConfigurationService ConfigurationService { get; set; }
        public ITelemetryService TelemetryService { get; set; }
        public AuthenticationService AuthenticationService { get; set; }
        public ICredentialBuilder CredentialBuilder { get; set; }
        protected ISecurityPolicyService SecurityPolicyService { get; set; }
        public IReservedNamespaceService ReservedNamespaceService { get; set; }
        public IPackageUploadService PackageUploadService { get; set; }

        protected ApiController()
        {
            AuditingService = NuGetGallery.Auditing.AuditingService.None;
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
            IAuditingService auditingService,
            IGalleryConfigurationService configurationService,
            ITelemetryService telemetryService,
            AuthenticationService authenticationService,
            ICredentialBuilder credentialBuilder,
            ISecurityPolicyService securityPolicies,
            IReservedNamespaceService reservedNamespaceService,
            IPackageUploadService packageUploadService)
        {
            EntitiesContext = entitiesContext;
            PackageService = packageService;
            PackageFileService = packageFileService;
            UserService = userService;
            NugetExeDownloaderService = nugetExeDownloaderService;
            ContentService = contentService;
            IndexingService = indexingService;
            SearchService = searchService;
            AutoCuratePackage = autoCuratePackage;
            StatusService = statusService;
            MessageService = messageService;
            AuditingService = auditingService;
            ConfigurationService = configurationService;
            TelemetryService = telemetryService;
            AuthenticationService = authenticationService;
            CredentialBuilder = credentialBuilder;
            SecurityPolicyService = securityPolicies;
            ReservedNamespaceService = reservedNamespaceService;
            PackageUploadService = packageUploadService;
            StatisticsService = null;
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
            IAuditingService auditingService,
            IGalleryConfigurationService configurationService,
            ITelemetryService telemetryService,
            AuthenticationService authenticationService,
            ICredentialBuilder credentialBuilder,
            ISecurityPolicyService securityPolicies,
            IReservedNamespaceService reservedNamespaceService,
            IPackageUploadService packageUploadService)
            : this(entitiesContext, packageService, packageFileService, userService, nugetExeDownloaderService, contentService,
                  indexingService, searchService, autoCuratePackage, statusService, messageService, auditingService,
                  configurationService, telemetryService, authenticationService, credentialBuilder, securityPolicies,
                  reservedNamespaceService, packageUploadService)
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
            if (!PackageIdValidator.IsValidPackageId(id ?? string.Empty))
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, "The format of the package ID is invalid");
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
                version = NuGetVersionFormatter.Normalize(version);
            }
            else
            {
                // If version is null, get the latest version from the database.
                // This ensures that on package restore scenario where version will be non null, we don't hit the database.
                try
                {
                    var package = PackageService.FindPackageByIdAndVersion(
                        id,
                        version,
                        SemVerLevelKey.SemVer2,
                        allowPrerelease: false);

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
        [ActionName("HealthProbeApi")]
        public ActionResult HealthProbe()
        {
            return new HttpStatusCodeWithBodyResult(HttpStatusCode.OK, "Gallery is Available");
        }

        [HttpPost]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
        [ActionName("CreatePackageVerificationKey")]
        public async virtual Task<ActionResult> CreatePackageVerificationKeyAsync(string id, string version)
        {
            // For backwards compatibility, we must preserve existing behavior where the client always pushes
            // symbols and the VerifyPackageKey callback returns the appropriate response. For this reason, we
            // always create a temp key scoped to the unverified package ID here and defer package and owner
            // validation until the VerifyPackageKey call.

            var user = GetCurrentUser();
            var credential = user.GetCurrentApiKeyCredential(User.Identity);
            var tempCredential = CredentialBuilder.CreatePackageVerificationApiKey(credential, id);

            await AuthenticationService.AddCredential(user, tempCredential);

            TelemetryService.TrackCreatePackageVerificationKeyEvent(id, version, user, User.Identity);

            return Json(new
            {
                Key = tempCredential.Value,
                Expires = tempCredential.Expires.Value.ToString("O")
            });
        }

        [HttpGet]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackageVerify, NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
        [ActionName("VerifyPackageKey")]
        public async virtual Task<ActionResult> VerifyPackageKeyAsync(string id, string version)
        {
            var policyResult = await SecurityPolicyService.EvaluateAsync(SecurityPolicyAction.PackageVerify, HttpContext);
            if (!policyResult.Success)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, policyResult.ErrorMessage);
            }

            var user = GetCurrentUser();
            var credential = user.GetCurrentApiKeyCredential(User.Identity);

            var result = await VerifyPackageKeyInternalAsync(user, credential, id, version);

            // Expire and delete verification key after first use to avoid growing the database tables.
            if (CredentialTypes.IsPackageVerificationApiKey(credential.Type))
            {
                await AuthenticationService.RemoveCredential(user, credential);
            }

            TelemetryService.TrackVerifyPackageKeyEvent(id, version, user, User.Identity, result?.StatusCode ?? 200);

            return (ActionResult)result ?? new EmptyResult();
        }

        private async Task<HttpStatusCodeWithBodyResult> VerifyPackageKeyInternalAsync(User user, Credential credential, string id, string version)
        {
            // Verify that the user has permission to push for the specific Id \ version combination.
            var package = PackageService.FindPackageByIdAndVersion(id, version, semVerLevelKey: SemVerLevelKey.SemVer2);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            // Write an audit record
            await AuditingService.SaveAuditRecordAsync(
                new PackageAuditRecord(package, AuditedPackageAction.Verify));
            
            string[] requestedActions;
            if (CredentialTypes.IsPackageVerificationApiKey(credential.Type))
            {
                requestedActions = new[] { NuGetScopes.PackageVerify };
            }
            else
            {
                requestedActions = new[] { NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion };
            }

            var apiScopeEvaluationResult = EvaluateApiScopeOnExisting(ActionsRequiringPermissions.VerifyPackage, package, out var owner, requestedActions);
            if (apiScopeEvaluationResult != ApiScopeEvaluationResult.Success)
            {
                return GetHttpResultFromFailedApiScopeEvaluation(apiScopeEvaluationResult, id, version);
            }

            return null;
        }

        [HttpPut]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePut()
        {
            return CreatePackageInternal();
        }

        [HttpPost]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
        [ActionName("PushPackageApi")]
        public virtual Task<ActionResult> CreatePackagePost()
        {
            return CreatePackageInternal();
        }

        private async Task<ActionResult> CreatePackageInternal()
        {
            var policyResult = await SecurityPolicyService.EvaluateAsync(SecurityPolicyAction.PackagePush, HttpContext);
            if (!policyResult.Success)
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, policyResult.ErrorMessage);
            }

            // Get the user
            var currentUser = GetCurrentUser();

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
                        try
                        {
                            PackageService.EnsureValid(packageToPush);
                        }
                        catch (Exception ex)
                        {
                            ex.Log();

                            var message = Strings.FailedToReadUploadFile;
                            if (ex is InvalidPackageException || ex is InvalidDataException || ex is EntityException)
                            {
                                message = ex.Message;
                            }

                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, message);
                        }

                        NuspecReader nuspec;
                        var errors = ManifestValidator.Validate(packageToPush.GetNuspec(), out nuspec).ToArray();
                        if (errors.Length > 0)
                        {
                            var errorsString = string.Join("', '", errors.Select(error => error.ErrorMessage));
                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, string.Format(
                                CultureInfo.CurrentCulture,
                                errors.Length > 1 ? Strings.UploadPackage_InvalidNuspecMultiple : Strings.UploadPackage_InvalidNuspec,
                                errorsString));
                        }

                        if (nuspec.GetMinClientVersion() > Constants.MaxSupportedMinClientVersion)
                        {
                            return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.UploadPackage_MinClientVersionOutOfRange,
                                nuspec.GetMinClientVersion()));
                        }

                        User owner;

                        // Ensure that the user can push packages for this partialId.
                        var id = nuspec.GetId();
                        var version = nuspec.GetVersion();
                        var packageRegistration = PackageService.FindPackageRegistrationById(id);
                        if (packageRegistration == null)
                        {
                            // Check if the current user's scopes allow pushing a new package ID
                            var apiScopeEvaluationResult = EvaluateApiScopeOnNew(id, out owner);
                            if (apiScopeEvaluationResult != ApiScopeEvaluationResult.Success)
                            {
                                // User cannot push a new package ID as the current user's scopes does not allow it
                                return GetHttpResultFromFailedApiScopeEvaluation(apiScopeEvaluationResult, id, version);
                            }
                        }
                        else
                        {
                            // Check if the current user's scopes allow pushing a new version of an existing package ID
                            var apiScopeEvaluationResult = EvaluateApiScopeOnExisting(ActionsRequiringPermissions.UploadNewPackageVersion, packageRegistration, out owner, NuGetScopes.PackagePushVersion, NuGetScopes.PackagePush);
                            if (apiScopeEvaluationResult != ApiScopeEvaluationResult.Success)
                            {
                                // User cannot push a package as the current user's scopes does not allow it
                                await AuditingService.SaveAuditRecordAsync(
                                    new FailedAuthenticatedOperationAuditRecord(
                                        currentUser.Username,
                                        AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner,
                                        attemptedPackage: new AuditedPackageIdentifier(
                                            id, version.ToNormalizedStringSafe())));

                                return GetHttpResultFromFailedApiScopeEvaluation(apiScopeEvaluationResult, id, version);
                            }

                            if (packageRegistration.IsLocked)
                            {
                                return new HttpStatusCodeWithBodyResult(
                                    HttpStatusCode.Forbidden,
                                    string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, packageRegistration.Id));
                            }

                            // Check if a particular Id-Version combination already exists. We eventually need to remove this check.
                            string normalizedVersion = version.ToNormalizedString();
                            bool packageExists =
                                packageRegistration.Packages.Any(
                                    p => string.Equals(
                                        p.NormalizedVersion,
                                        normalizedVersion,
                                        StringComparison.OrdinalIgnoreCase));

                            if (packageExists)
                            {
                                return new HttpStatusCodeWithBodyResult(
                                    HttpStatusCode.Conflict,
                                    string.Format(CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified,
                                        id, nuspec.GetVersion().ToNormalizedStringSafe()));
                            }
                        }

                        var packageStreamMetadata = new PackageStreamMetadata
                        {
                            HashAlgorithm = Constants.Sha512HashAlgorithmId,
                            Hash = CryptographyService.GenerateHash(packageStream.AsSeekableStream()),
                            Size = packageStream.Length
                        };

                        var package = await PackageUploadService.GeneratePackageAsync(
                            id,
                            packageToPush,
                            packageStreamMetadata,
                            owner,
                            currentUser);

                        await AutoCuratePackage.ExecuteAsync(package, packageToPush, commitChanges: false);

                        PackageCommitResult commitResult;
                        using (Stream uploadStream = packageStream)
                        {
                            uploadStream.Position = 0;
                            commitResult = await PackageUploadService.CommitPackageAsync(
                                package,
                                uploadStream.AsSeekableStream());
                        }

                        switch (commitResult)
                        {
                            case PackageCommitResult.Success:
                                break;
                            case PackageCommitResult.Conflict:
                                return new HttpStatusCodeWithBodyResult(
                                    HttpStatusCode.Conflict,
                                    Strings.UploadPackage_IdVersionConflict);
                            default:
                                throw new NotImplementedException($"The package commit result {commitResult} is not supported.");
                        }

                        IndexingService.UpdatePackage(package);

                        // Write an audit record
                        await AuditingService.SaveAuditRecordAsync(
                            new PackageAuditRecord(package, AuditedPackageAction.Create, PackageCreatedVia.Api));

                        if (!(ConfigurationService.Current.AsynchronousPackageValidationEnabled && ConfigurationService.Current.BlockingAsynchronousPackageValidationEnabled))
                        {
                            // Notify user of push unless async validation in blocking mode is used
                            MessageService.SendPackageAddedNotice(package,
                                Url.Package(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                                Url.ReportPackage(package.PackageRegistration.Id, package.NormalizedVersion, relativeUrl: false),
                                Url.AccountSettings(relativeUrl: false));
                        }

                        TelemetryService.TrackPackagePushEvent(package, currentUser, User.Identity);

                        if (package.SemVerLevelKey == SemVerLevelKey.SemVer2)
                        {
                            return new HttpStatusCodeWithServerWarningResult(HttpStatusCode.Created, Strings.WarningSemVer2PackagePushed);
                        }

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
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackageUnlist)]
        [ActionName("DeletePackageApi")]
        public virtual async Task<ActionResult> DeletePackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            // Check if the current user's scopes allow listing/unlisting the current package ID
            var apiScopeEvaluationResult = EvaluateApiScopeOnExisting(ActionsRequiringPermissions.UnlistOrRelistPackage, package, out var owner, NuGetScopes.PackageUnlist);
            if (apiScopeEvaluationResult != ApiScopeEvaluationResult.Success)
            {
                return GetHttpResultFromFailedApiScopeEvaluation(apiScopeEvaluationResult, id, version);
            }

            if (package.PackageRegistration.IsLocked)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden,
                    string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id));
            }

            await PackageService.MarkPackageUnlistedAsync(package);
            IndexingService.UpdatePackage(package);
            return new EmptyResult();
        }

        [HttpPost]
        [ApiAuthorize]
        [ApiScopeRequired(NuGetScopes.PackageUnlist)]
        [ActionName("PublishPackageApi")]
        public virtual async Task<ActionResult> PublishPackage(string id, string version)
        {
            var package = PackageService.FindPackageByIdAndVersionStrict(id, version);
            if (package == null)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, id, version));
            }

            // Check if the current user's scopes allow listing/unlisting the current package ID
            var apiScopeEvaluationResult = EvaluateApiScopeOnExisting(ActionsRequiringPermissions.UnlistOrRelistPackage, package, out var owner, NuGetScopes.PackageUnlist);
            if (apiScopeEvaluationResult != ApiScopeEvaluationResult.Success)
            {
                return GetHttpResultFromFailedApiScopeEvaluation(apiScopeEvaluationResult, id, version);
            }

            if (package.PackageRegistration.IsLocked)
            {
                return new HttpStatusCodeWithBodyResult(
                    HttpStatusCode.Forbidden,
                    string.Format(CultureInfo.CurrentCulture, Strings.PackageIsLocked, package.PackageRegistration.Id));
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
        public virtual async Task<ActionResult> GetPackageIds(
            string partialId,
            bool? includePrerelease,
            string semVerLevel = null)
        {
            var query = GetService<IAutoCompletePackageIdsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(partialId, includePrerelease, semVerLevel)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("PackageVersions")]
        public virtual async Task<ActionResult> GetPackageVersions(
            string id,
            bool? includePrerelease,
            string semVerLevel = null)
        {
            var query = GetService<IAutoCompletePackageVersionsQuery>();
            return new JsonResult
            {
                Data = (await query.Execute(id, includePrerelease, semVerLevel)).ToArray(),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpGet]
        [ActionName("StatisticsDownloadsApi")]
        public virtual async Task<ActionResult> GetStatsDownloads(int? count)
        {
            await StatisticsService.Refresh();

            if (StatisticsService.PackageVersionDownloadsResult.IsLoaded)
            {
                int i = 0;

                JArray content = new JArray();
                foreach (StatisticsPackagesItemViewModel row in StatisticsService.PackageVersionDownloads)
                {
                    JObject item = new JObject();

                    item.Add("PackageId", row.PackageId);
                    item.Add("PackageVersion", row.PackageVersion);
                    item.Add("Gallery", Url.Package(row.PackageId, row.PackageVersion));
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

        /// <summary>
        /// The result of evaluating the current user's scopes.
        /// </summary>
        /// <remarks>
        /// When an current user's scopes are evaluated and none evaluate with <see cref="Success"/>, 
        /// the failed result to return is determined by <see cref="ChooseFailureResult(ApiScopeEvaluationResult, ApiScopeEvaluationResult)"/>.
        /// </remarks>
        private enum ApiScopeEvaluationResult
        {
            /// <summary>
            /// An error occurred and scopes were unable to be evaluated.
            /// </summary>
            Unknown,

            /// <summary>
            /// The scopes evaluated successfully.
            /// </summary>
            Success,

            /// <summary>
            /// The scopes do not match the action being performed.
            /// </summary>
            Forbidden,

            /// <summary>
            /// The scopes match the action being performed, but there is a reserved namespace conflict that prevents this action from being successful.
            /// </summary>
            ConflictReservedNamespace
        }

        private HttpStatusCodeWithBodyResult GetHttpResultFromFailedApiScopeEvaluation(ApiScopeEvaluationResult evaluationResult, string id, string version)
        {
            return GetHttpResultFromFailedApiScopeEvaluation(evaluationResult, id, NuGetVersion.Parse(version));
        }

        private HttpStatusCodeWithBodyResult GetHttpResultFromFailedApiScopeEvaluation(ApiScopeEvaluationResult evaluationResult, string id, NuGetVersion version)
        {
            switch (evaluationResult)
            {
                case ApiScopeEvaluationResult.Success:
                    throw new ArgumentException($"{nameof(ApiScopeEvaluationResult.Success)} is not a failed evaluation!", nameof(evaluationResult));

                case ApiScopeEvaluationResult.Forbidden:
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);

                case ApiScopeEvaluationResult.ConflictReservedNamespace:
                    TelemetryService.TrackPackagePushNamespaceConflictEvent(id, version.ToNormalizedString(), GetCurrentUser(), User.Identity);
                    return new HttpStatusCodeWithBodyResult(HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);

                default:
                    throw new ArgumentException("Unsupported evaluation result!", nameof(evaluationResult));
            }
        }

        /// <summary>
        /// Evaluates the current user's scopes against <paramref name="requestedActions"/> and an existing package ID specified by <paramref name="package"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the .</returns>
        private ApiScopeEvaluationResult EvaluateApiScopeOnExisting(ActionRequiringPackagePermissions action, Package package, out User owner, params string[] requestedActions)
        {
            return EvaluateApiScopeOnExisting(action, package.PackageRegistration, out owner, requestedActions);
        }

        /// <summary>
        /// Evaluates the current user's scopes against <paramref name="requestedActions"/> and an existing package ID specified by <paramref name="packageRegistration"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the .</returns>
        private ApiScopeEvaluationResult EvaluateApiScopeOnExisting(ActionRequiringPackagePermissions action, PackageRegistration packageRegistration, out User owner, params string[] requestedActions)
        {
            return EvaluateApiScope(new ExistingScopeSubject(action, packageRegistration), out owner, requestedActions);
        }

        /// <summary>
        /// Evaluates the current user's scopes against a new package ID specified by <paramref name="id"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the .</returns>
        private ApiScopeEvaluationResult EvaluateApiScopeOnNew(string id, out User owner)
        {
            return EvaluateApiScope(
                new NewScopeSubject(ActionsRequiringPermissions.UploadNewPackageId, new ActionOnNewPackageContext(id, ReservedNamespaceService)),
                out owner, NuGetScopes.PackagePush);
        }

        /// <summary>
        /// Helps evaluate whether or not the current user has the correct <see cref="Scope"/>s to perform an action on a subject.
        /// </summary>
        private interface IScopeSubject
        {
            bool IsSubjectAllowedByScope(Scope scope);
            PermissionsCheckResult CheckPermissions(User currentUser, User owner);
        }

        /// <summary>
        /// An <see cref="IScopeSubject"/> to help evaluate scopes against a new package ID.
        /// </summary>
        private class NewScopeSubject : IScopeSubject
        {
            private readonly ActionRequiringReservedNamespacePermissions _action;
            private readonly ActionOnNewPackageContext _context;
            
            public NewScopeSubject(ActionRequiringReservedNamespacePermissions action, ActionOnNewPackageContext context)
            {
                _action = action;
                _context = context;
            }

            public bool IsSubjectAllowedByScope(Scope scope)
            {
                return scope.AllowsSubject(_context.PackageId);
            }

            public PermissionsCheckResult CheckPermissions(User currentUser, User owner)
            {
                return _action.CheckPermissions(currentUser, owner, _context);
            }
        }

        /// <summary>
        /// An <see cref="IScopeSubject"/> to help evaluate scopes against an existing package ID.
        /// </summary>
        private class ExistingScopeSubject : IScopeSubject
        {
            private readonly ActionRequiringPackagePermissions _action;
            private readonly PackageRegistration _packageRegistration;

            public ExistingScopeSubject(ActionRequiringPackagePermissions action, PackageRegistration packageRegistration)
            {
                _action = action;
                _packageRegistration = packageRegistration;
            }

            public bool IsSubjectAllowedByScope(Scope scope)
            {
                return scope.AllowsSubject(_packageRegistration.Id);
            }

            public PermissionsCheckResult CheckPermissions(User currentUser, User owner)
            {
                return _action.CheckPermissions(currentUser, owner, _packageRegistration);
            }
        }

        /// <summary>
        /// Evaluates the current user's scopes against <paramref name="scopeSubject"/> and <paramref name="requestedActions"/>.
        /// </summary>
        /// <param name="owner">
        /// The <see cref="User"/> specified by the <see cref="Scope.OwnerKey"/> of the <see cref="Scope"/> that evaluated with <see cref="ApiScopeEvaluationResult.Success"/>.
        /// If no <see cref="Scope"/>s evaluate to <see cref="ApiScopeEvaluationResult.Success"/>, this will be null.
        /// </param>
        /// <returns>A <see cref="ApiScopeEvaluationResult"/> that describes the evaluation of the .</returns>
        private ApiScopeEvaluationResult EvaluateApiScope(IScopeSubject scopeSubject, out User owner, params string[] requestedActions)
        {
            owner = null;

            var currentUser = GetCurrentUser();
            IEnumerable<Scope> scopes = User.Identity.GetScopesFromClaim();

            if (scopes == null || !scopes.Any())
            {
                // Legacy V1 API key without scopes.
                // Evaluate it as if it has an unlimited scope.
                scopes = new[] { new Scope(ownerKey: null, subject: NuGetPackagePattern.AllInclusivePattern, allowedAction: NuGetScopes.All) };
            }

            var failureResult = ApiScopeEvaluationResult.Unknown;

            foreach (var scope in scopes)
            {
                if (!scopeSubject.IsSubjectAllowedByScope(scope))
                {
                    // Subject (package ID) does not match.
                    failureResult = ChooseFailureResult(failureResult, ApiScopeEvaluationResult.Forbidden);
                    continue;
                }

                if (!scope.AllowsActions(requestedActions))
                {
                    // Action scopes does not match.
                    failureResult = ChooseFailureResult(failureResult, ApiScopeEvaluationResult.Forbidden);
                    continue;
                }

                // Get the owner from the scope.
                // If the scope has no owner, use the current user.
                var ownerInScope = scope.HasOwnerScope() ? UserService.FindByKey(scope.OwnerKey.Value) : currentUser;

                var isActionAllowed = scopeSubject.CheckPermissions(currentUser, ownerInScope);
                if (isActionAllowed != PermissionsCheckResult.Allowed)
                {
                    // Current user cannot do the action on behalf of the owner in the scope or owner in the scope is not allowed to do the action.
                    var currentFailureResult = ApiScopeEvaluationResult.Forbidden;
                    if (isActionAllowed == PermissionsCheckResult.ReservedNamespaceFailure)
                    {
                        currentFailureResult = ApiScopeEvaluationResult.ConflictReservedNamespace;
                    }

                    failureResult = ChooseFailureResult(failureResult, currentFailureResult);
                    continue;
                }

                owner = ownerInScope;
                return ApiScopeEvaluationResult.Success;
            }

            return failureResult;
        }

        /// <summary>
        /// Determines the <see cref="ApiScopeEvaluationResult"/> to return from <see cref="EvaluateApiScope(IScopeSubject, out User, string[])"/> when no <see cref="Scope"/>s return <see cref="ApiScopeEvaluationResult.Success"/>.
        /// </summary>
        /// <param name="last">The result of the <see cref="Scope"/>s that have been evaluated so far.</param>
        /// <param name="next">The result of the <see cref="Scope"/> that was just evaluated.</param>
        private ApiScopeEvaluationResult ChooseFailureResult(ApiScopeEvaluationResult last, ApiScopeEvaluationResult next)
        {
            return new[] { last, next }.Max();
        }
    }
}
