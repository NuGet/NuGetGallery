﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Configuration;
using NuGetGallery.Filters;
using NuGetGallery.Helpers;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Packaging;
using PoliteCaptcha;
using RestSharp;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Web.Configuration;

namespace NuGetGallery
{
    public partial class PackagesController
        : AppController
    {
        // TODO: add support for URL-based package submission
        // TODO: add support for uploading logos and screenshots
        // TODO: improve validation summary emphasis

        private readonly IAutomaticallyCuratePackageCommand _autoCuratedPackageCmd;
        private readonly IAppConfiguration _config;
        private readonly IMessageService _messageService;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly ISearchService _searchService;
        private readonly IUploadFileService _uploadFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IIndexingService _indexingService;
        private readonly ICacheService _cacheService;
        private readonly EditPackageService _editPackageService;
        private readonly IPackageDeleteService _packageDeleteService;

        public PackagesController(
            IPackageService packageService,
            IUploadFileService uploadFileService,
            IMessageService messageService,
            ISearchService searchService,
            IAutomaticallyCuratePackageCommand autoCuratedPackageCmd,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IAppConfiguration config,
            IIndexingService indexingService,
            ICacheService cacheService,
            EditPackageService editPackageService,
            IPackageDeleteService packageDeleteService)
        {
            _packageService = packageService;
            _uploadFileService = uploadFileService;
            _messageService = messageService;
            _searchService = searchService;
            _autoCuratedPackageCmd = autoCuratedPackageCmd;
            _packageFileService = packageFileService;
            _entitiesContext = entitiesContext;
            _config = config;
            _indexingService = indexingService;
            _cacheService = cacheService;
            _editPackageService = editPackageService;
            _packageDeleteService = packageDeleteService;
        }

        [Authorize]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public virtual ActionResult UploadPackageProgress()
        {
            string username = User.Identity.Name;

            AsyncFileUploadProgress progress = _cacheService.GetProgress(username);
            if (progress == null)
            {
                return HttpNotFound();
            }
            return Json(progress, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("undo pending edits")]
        [ValidateAntiForgeryToken]
        public virtual ActionResult UndoPendingEdits(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            // To do as much successful cancellation as possible, Will not batch, but will instead try to cancel
            // pending edits 1 at a time, starting with oldest first.
            var pendingEdits = _entitiesContext.Set<PackageEdit>()
                .Where(pe => pe.PackageKey == package.Key)
                .OrderBy(pe => pe.Timestamp)
                .ToList();

            int numOK = 0;
            int numConflicts = 0;
            foreach (var result in pendingEdits)
            {
                try
                {
                    _entitiesContext.DeleteOnCommit(result);
                    _entitiesContext.SaveChanges();
                    numOK += 1;
                }
                catch (DataException)
                {
                    numConflicts += 1;
                }
            }

            if (numConflicts > 0)
            {
                TempData["Message"] = "Your pending edit has already been completed and could not be canceled.";
            }
            else if (numOK > 0)
            {
                TempData["Message"] = "Your pending edits for this package were successfully canceled.";
            }
            else
            {
                TempData["Message"] = "No pending edits were found for this package. The edits may have already been completed.";
            }

            return Redirect(Url.Package(id, version));
        }

        [Authorize]
        [RequiresAccountConfirmation("upload a package")]
        public async virtual Task<ActionResult> UploadPackage()
        {
            var currentUser = GetCurrentUser();

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return RedirectToRoute(RouteName.VerifyPackage);
                }
            }

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<ActionResult> UploadPackage(HttpPostedFileBase uploadFile)
        {
            var currentUser = GetCurrentUser();

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    return new HttpStatusCodeResult(409, "Cannot upload file because an upload is already in progress.");
                }
            }

            if (uploadFile == null)
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileIsRequired);
                return View();
            }

            if (!Path.GetExtension(uploadFile.FileName).Equals(Constants.NuGetPackageFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(String.Empty, Strings.UploadFileMustBeNuGetPackage);
                return View();
            }

            using (var uploadStream = uploadFile.InputStream)
            {
                INupkg nuGetPackage;
                try
                {
                    nuGetPackage = CreatePackage(uploadStream);
                }
                catch (InvalidPackageException ipex)
                {
                    ipex.Log();
                    ModelState.AddModelError(String.Empty, ipex.Message);
                    return View();
                }
                catch (InvalidDataException idex)
                {
                    idex.Log();
                    ModelState.AddModelError(String.Empty, idex.Message);
                    return View();
                }
                catch (Exception ex)
                {
                    ex.Log();
                    ModelState.AddModelError(String.Empty, Strings.FailedToReadUploadFile);
                    return View();
                }
                finally
                {
                    _cacheService.RemoveProgress(currentUser.Username);
                }

                var errors = ManifestValidator.Validate(nuGetPackage).ToArray();
                if (errors.Length > 0)
                {
                    foreach (var error in errors)
                    {
                        ModelState.AddModelError(String.Empty, error.ErrorMessage);
                    }
                    return View();
                }

                // Check min client version
                if (nuGetPackage.Metadata.MinClientVersion > Constants.MaxSupportedMinClientVersion)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.UploadPackage_MinClientVersionOutOfRange,
                            nuGetPackage.Metadata.MinClientVersion));
                    return View();
                }

                var packageRegistration = _packageService.FindPackageRegistrationById(nuGetPackage.Metadata.Id);
                if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
                {
                    ModelState.AddModelError(
                        string.Empty, string.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                    return View();
                }

                var package = _packageService.FindPackageByIdAndVersion(nuGetPackage.Metadata.Id, nuGetPackage.Metadata.Version.ToStringSafe());
                if (package != null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        string.Format(
                            CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                    return View();
                }

                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, nuGetPackage.GetStream());
            }

            return RedirectToRoute(RouteName.VerifyPackage);
        }

        public virtual async Task<ActionResult> DisplayPackage(string id, string version)
        {
            string normalized = SemanticVersionExtensions.Normalize(version);
            if (!string.Equals(version, normalized))
            {
                // Permanent redirect to the normalized one (to avoid multiple URLs for the same content)
                return RedirectToActionPermanent("DisplayPackage", new { id = id, version = normalized });
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }
            var model = new DisplayPackageViewModel(package);

            if (package.IsOwner(User))
            {
                // Tell logged-in package owners not to cache the package page,
                // so they won't be confused about the state of pending edits.
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
                Response.Cache.SetMaxAge(TimeSpan.Zero);
                Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);

                var pendingMetadata = _editPackageService.GetPendingMetadata(package);
                if (pendingMetadata != null)
                {
                    model.SetPendingMetadata(pendingMetadata);
                }
            }

            var externalSearchService = _searchService as ExternalSearchService;
            if (_searchService.ContainsAllVersions && externalSearchService != null)
            {
                var isIndexedCacheKey = string.Format("IsIndexed_{0}_{1}", package.PackageRegistration.Id, package.Version);
                var isIndexed = HttpContext.Cache.Get(isIndexedCacheKey) as bool?;
                if (!isIndexed.HasValue)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(
                            "id:\"" + package.PackageRegistration.Id + "\" AND version:\"" + package.Version + "\"",
                            1, null, SearchFilter.ODataSearchContext);
                    var results = await externalSearchService.RawSearch(searchFilter);

                    isIndexed = results.Hits > 0;

                    var expiration = Cache.NoAbsoluteExpiration;
                    if (!isIndexed.Value)
                    {
                        expiration = DateTime.UtcNow.Add(TimeSpan.FromSeconds(30));
                    }

                    HttpContext.Cache.Add(isIndexedCacheKey,
                        isIndexed,
                        null,
                        expiration,
                        Cache.NoSlidingExpiration,
                        CacheItemPriority.Default, null);
                }

                model.IsIndexed = isIndexed;
            }

            ViewBag.FacebookAppID = _config.FacebookAppId;
            return View(model);
        }

        public virtual async Task<ActionResult> ListPackages(string q, int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            q = (q ?? string.Empty).Trim();

            SearchResults results;

            // fetch most common query from cache to relieve load on the search service
            if (string.IsNullOrEmpty(q) && page == 1)
            {
                var cachedResults = HttpContext.Cache.Get("DefaultSearchResults");
                if (cachedResults == null)
                {
                    var searchFilter = SearchAdaptor.GetSearchFilter(q, page, null, SearchFilter.UISearchContext);
                    results = await _searchService.Search(searchFilter);

                    // note: this is a per instance cache
                    HttpContext.Cache.Add(
                        "DefaultSearchResults",
                        results,
                        null,
                        DateTime.UtcNow.AddMinutes(10),
                        Cache.NoSlidingExpiration,
                        CacheItemPriority.Default, null);
                }
                else
                {
                    // default for /packages view
                    results = (SearchResults)cachedResults;
                }
            }
            else
            {
                var searchFilter = SearchAdaptor.GetSearchFilter(q, page, null, SearchFilter.UISearchContext);
                results = await _searchService.Search(searchFilter);
            }

            int totalHits = results.Hits;
            if (page == 1 && !results.Data.Any())
            {
                // In the event the index wasn't updated, we may get an incorrect count.
                totalHits = 0;
            }

            var viewModel = new PackageListViewModel(
                results.Data,
                results.IndexTimestampUtc,
                q,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        private void AddNewSupportRequest(string subject, ReportPackageRequest request, Package package, User user, ReportAbuseViewModel reportForm)
        {
            try
            {
                subject = request.FillIn(subject, _config);
                var admin = new Areas.Admin.Models.AdminModel();

                var issue = new Areas.Admin.Models.IssueModel();
                var newIssue = new Areas.Admin.Models.Issue();
                var primaryOnCall = GetPrimaryOnCall();
                newIssue.AssignedTo = (primaryOnCall == String.Empty) ? 0 : admin.GetAdminKeyFromUserName(primaryOnCall);

                newIssue.CreatedDate = DateTime.Now;
                newIssue.Details = reportForm.Message;
                newIssue.IssueStatus = 1;
                newIssue.IssueTitle = subject;
                if (user != null)
                {
                    newIssue.CreatedBy = user.Username;
                    newIssue.OwnerEmail = user.EmailAddress;
                }
                else
                {
                    newIssue.CreatedBy = "Anonymous";
                    newIssue.OwnerEmail = reportForm.Email;
                }
                newIssue.PackageID = package.PackageRegistration.Id;
                newIssue.PackageVersion = package.Version;
                newIssue.Reason = reportForm.Reason.ToString();
                newIssue.SiteRoot = _config.SiteRoot;
                issue.AddIssue(newIssue);
            }
            catch (System.Data.SqlClient.SqlException)
            {
                var errorMessage = String.Format("Exception thrown while logging a support request into DB for {0}, {1} at {2}",
                                                    package.PackageRegistration.Id,
                                                    package.Version,
                                                    DateTime.Now);
                TriggerAPagerDutyIncident(errorMessage);
            }
            catch (Exception) //In case getting data from PagerDuty has failed
            {
                //swallow
            }
        }

        private void TriggerAPagerDutyIncident(string errorMeesage)
        {
            try
            {
                var pagerDutyAPIKey = WebConfigurationManager.AppSettings["Gallery.PagerDutyAPIKey"];
                var pagerDutyServiceKey = WebConfigurationManager.AppSettings["Gallery.PagerDutyServiceKey"];

                var client = new RestClient(); //("URL/create_event.json");
                var pagerDutyIncidentTriggerURL = WebConfigurationManager.AppSettings["Gallery.PagerDutyIncidentTriggerURL"];
                client.BaseUrl = new Uri(pagerDutyIncidentTriggerURL);

                var request = new RestRequest();
                request.AddHeader("Content-Type", "application/json; charset=utf-8");
                request.AddHeader("Authorization", "Token token=" + pagerDutyAPIKey);

                request.RequestFormat = DataFormat.Json;
                request.AddBody(
                new
                {
                    service_key = pagerDutyServiceKey,
                    event_type = "trigger",
                    description = errorMeesage
                });

                request.Method = Method.POST;

                var response = client.Execute(request) as RestResponse;
            }
            catch (Exception)
            {

                //swallow?
            }
        }

        private string GetPrimaryOnCall()
        {
            var pagerDutyAPIKey = WebConfigurationManager.AppSettings["Gallery.PagerDutyAPIKey"];
            var client = new RestClient(); //("URL/create_event.json");
            var pagerDutyOnCallURL = WebConfigurationManager.AppSettings["Gallery.PagerDutyOnCallURL"];
            client.BaseUrl = new Uri(pagerDutyOnCallURL);

            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddHeader("Authorization", "Token token=" + pagerDutyAPIKey);

            var returnVal = String.Empty;
            request.RequestFormat = DataFormat.Json;
            request.Method = Method.GET;
            var response = client.Execute(request) as RestResponse;

            if (response != null && ((response.StatusCode == HttpStatusCode.OK) &&

                (response.ResponseStatus == ResponseStatus.Completed)))
            {

                var content = response.Content;
                var root = JObject.Parse(content);
                var users = (JArray)root["users"];

                foreach (var item in users)
                {
                    var on_call = item["on_call"][0];
                    if (Convert.ToInt32(on_call["level"]) == 1)
                    {
                        var email = item["email"].ToString();
                        var length = email.IndexOf("@");
                        returnVal = email.Substring(0, length);
                        break;
                    }
                }
            }
            return returnVal;
        }

        // NOTE: Intentionally NOT requiring authentication
        private static readonly ReportPackageReason[] ReportOtherPackageReasons = new[] {
            ReportPackageReason.IsFraudulent,
            ReportPackageReason.ViolatesALicenseIOwn,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.HasABugOrFailedToInstall,
            ReportPackageReason.Other
        };

        public virtual ActionResult ReportAbuse(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportOtherPackageReasons,
                PackageId = id,
                PackageVersion = package.Version,
                CopySender = true,
            };

            if (Request.IsAuthenticated)
            {
                var user = GetCurrentUser();

                // If user logged on in as owner a different tab, then clicked the link, we can redirect them to ReportMyPackage
                if (package.IsOwner(user))
                {
                    return RedirectToAction("ReportMyPackage", new { id, version });
                }

                if (user.Confirmed)
                {
                    model.ConfirmedUser = true;
                }
            }

            ViewData[Constants.ReturnUrlViewDataKey] = Url.Action("ReportMyPackage", new { id, version });
            return View(model);
        }

        private static readonly ReportPackageReason[] ReportMyPackageReasons = {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.PublishedWithWrongVersion,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        [Authorize]
        [RequiresAccountConfirmation("contact support about your package")]
        public virtual ActionResult ReportMyPackage(string id, string version)
        {
            var user = GetCurrentUser();

            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return HttpNotFound();
            }

            // If user hit this url by constructing it manually but is not the owner, redirect them to ReportAbuse
            if (!(User.IsInRole(Constants.AdminRoleName) || package.IsOwner(user)))
            {
                return RedirectToAction("ReportAbuse", new { id, version });
            }

            var model = new ReportAbuseViewModel
            {
                ReasonChoices = ReportMyPackageReasons,
                ConfirmedUser = user.Confirmed,
                PackageId = id,
                PackageVersion = package.Version,
                CopySender = true,
                Signature = user.Username
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual ActionResult ReportAbuse(string id, string version, ReportAbuseViewModel reportForm)
        {
            // Html Encode the message
            reportForm.Message = System.Web.HttpUtility.HtmlEncode(reportForm.Message);

            if (!ModelState.IsValid)
            {
                return ReportAbuse(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            User user = null;
            MailAddress from;
            if (Request.IsAuthenticated)
            {
                user = GetCurrentUser();
                from = user.ToMailAddress();
            }
            else
            {
                from = new MailAddress(reportForm.Email);
            }

            var request = new ReportPackageRequest
            {
                AlreadyContactedOwners = reportForm.AlreadyContactedOwner,
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                Url = Url,
                CopySender = reportForm.CopySender,
                Signature = reportForm.Signature
            };
            _messageService.ReportAbuse(request);
            string subject = "[{GalleryOwnerName}] Support Request for '{Id}' version {Version} (Reason: {Reason})";
            AddNewSupportRequest(subject, request, package, user, reportForm);

            TempData["Message"] = "Your abuse report has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
        }

        [HttpPost]
        [Authorize]
        [RequiresAccountConfirmation("contact support about your package")]
        [ValidateAntiForgeryToken]
        [ValidateSpamPrevention]
        public virtual ActionResult ReportMyPackage(string id, string version, ReportAbuseViewModel reportForm)
        {
            // Html Encode the message
            reportForm.Message = System.Web.HttpUtility.HtmlEncode(reportForm.Message);

            if (!ModelState.IsValid)
            {
                return ReportMyPackage(id, version);
            }

            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            MailAddress from = user.ToMailAddress();

            var request = new ReportPackageRequest
            {
                FromAddress = from,
                Message = reportForm.Message,
                Package = package,
                Reason = EnumHelper.GetDescription(reportForm.Reason.Value),
                RequestingUser = user,
                Url = Url,
                CopySender = reportForm.CopySender
            };

            _messageService.ReportMyPackage(request);

            string subject = "[{GalleryOwnerName}] Owner Support Request for '{Id}' version {Version} (Reason: {Reason})";
            AddNewSupportRequest(subject, request, package, user, reportForm);

            TempData["Message"] = "Your support request has been sent to the gallery operators.";
            return Redirect(Url.Package(id, version));
        }

        [Authorize]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual ActionResult ContactOwners(string id)
        {
            var package = _packageService.FindPackageRegistrationById(id);

            if (package == null)
            {
                return HttpNotFound();
            }

            var model = new ContactOwnersViewModel
            {
                PackageId = package.Id,
                Owners = package.Owners.Where(u => u.EmailAllowed),
                CopySender = true,
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("contact package owners")]
        public virtual ActionResult ContactOwners(string id, ContactOwnersViewModel contactForm)
        {
            // Html Encode the message
            contactForm.Message = System.Web.HttpUtility.HtmlEncode(contactForm.Message);

            if (!ModelState.IsValid)
            {
                return ContactOwners(id);
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            var fromAddress = new MailAddress(user.EmailAddress, user.Username);
            _messageService.SendContactOwnersMessage(
                fromAddress,
                package,
                contactForm.Message,
                Url.Action(
                    actionName: "Account",
                    controllerName: "Users",
                    routeValues: null,
                    protocol: Request.Url.Scheme),
                contactForm.CopySender);

            string message = String.Format(CultureInfo.CurrentCulture, "Your message has been sent to the owners of {0}.", id);
            TempData["Message"] = message;
            return RedirectToAction(
                actionName: "DisplayPackage",
                controllerName: "Packages",
                routeValues: new
                {
                    id,
                    version = (string)null
                });
        }

        // This is the page that explains why there's no download link.
        public virtual ActionResult Download()
        {
            return View();
        }

        [Authorize]
        public virtual ActionResult ManagePackageOwners(string id)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, string.Empty);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new ManagePackageOwnersViewModel(package, User);

            return View(model);
        }

        [Authorize]
        [RequiresAccountConfirmation("delete a package")]
        public virtual ActionResult Delete(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            var model = new DeletePackageViewModel(package, ReportMyPackageReasons);
            return View(model);
        }

        [Authorize(Roles = "Admins")]
        [HttpPost]
        [RequiresAccountConfirmation("delete a package")]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> Delete(DeletePackagesRequest deletePackagesRequest)
        {
            var packagesToDelete = new List<Package>();

            if (ModelState.IsValid)
            {
                // Get the packages to delete
                foreach (var package in deletePackagesRequest.Packages)
                {
                    var split = package.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length == 2)
                    {
                        var packageToDelete = _packageService.FindPackageByIdAndVersion(split[0], split[1], allowPrerelease: true);
                        if (packageToDelete != null)
                        {
                            packagesToDelete.Add(packageToDelete);
                        }
                    }
                }

                // Perform delete
                if (deletePackagesRequest.SoftDelete)
                {
                    await _packageDeleteService.SoftDeletePackagesAsync(
                        packagesToDelete, GetCurrentUser(), EnumHelper.GetDescription(deletePackagesRequest.Reason.Value),
                        deletePackagesRequest.Signature);
                }
                else
                {
                    await _packageDeleteService.HardDeletePackagesAsync(
                        packagesToDelete, GetCurrentUser(), EnumHelper.GetDescription(deletePackagesRequest.Reason.Value),
                        deletePackagesRequest.Signature,
                        deletePackagesRequest.DeleteEmptyPackageRegistration);
                }

                // Redirect out
                TempData["Message"] = 
                    "We're performing the package delete right now. It may take a while for this change to propagate through our system.";
                
                return Redirect("/");
            }

            if (!deletePackagesRequest.Packages.Any())
            {
                return HttpNotFound();
            }

            var firstPackage = packagesToDelete.First();
            return Delete(firstPackage.PackageRegistration.Id, firstPackage.Version);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("unlist a package")]
        [ValidateAntiForgeryToken]
        public virtual ActionResult UpdateListed(string id, string version, bool? listed)
        {
            // Edit does exactly the same thing that Delete used to do... REUSE ALL THE CODE!
            return Edit(id, version, listed, Url.Package);
        }

        [Authorize]
        [RequiresAccountConfirmation("edit a package")]
        public virtual ActionResult Edit(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            var packageRegistration = _packageService.FindPackageRegistrationById(id);
            var model = new EditPackageRequest
            {
                PackageId = package.PackageRegistration.Id,
                PackageTitle = package.Title,
                Version = package.Version,
                PackageVersions = packageRegistration.Packages
                    .OrderByDescending(p => new SemanticVersion(p.Version), Comparer<SemanticVersion>.Create((a, b) => a.CompareTo(b)))
                    .ToList(),
            };

            var pendingMetadata = _editPackageService.GetPendingMetadata(package);
            model.Edit = new EditPackageVersionRequest(package, pendingMetadata);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        [ValidateAntiForgeryToken]
        [RequiresAccountConfirmation("edit a package")]
        public virtual ActionResult Edit(string id, string version, EditPackageRequest formData, string returnUrl)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }

            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(403, "Forbidden");
            }

            var user = GetCurrentUser();
            if (!ModelState.IsValid)
            {
                formData.PackageId = package.PackageRegistration.Id;
                formData.PackageTitle = package.Title;
                formData.Version = package.Version;

                var packageRegistration = _packageService.FindPackageRegistrationById(id);
                formData.PackageVersions = packageRegistration.Packages
                        .OrderByDescending(p => new SemanticVersion(p.Version), Comparer<SemanticVersion>.Create((a, b) => a.CompareTo(b)))
                        .ToList();

                return View(formData);
            }

            // Add the edit request to a queue where it will be processed in the background.
            if (formData.Edit != null)
            {
                _editPackageService.StartEditPackageRequest(package, formData.Edit, user);
                _entitiesContext.SaveChanges();
            }

            return SafeRedirect(returnUrl ?? Url.Package(id, version));
        }

        [Authorize]
        [RequiresAccountConfirmation("accept ownership of a package")]
        public virtual ActionResult ConfirmOwner(string id, string username, string token)
        {
            if (String.IsNullOrEmpty(token))
            {
                return HttpNotFound();
            }

            if (!String.Equals(username, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                return View(new PackageOwnerConfirmationModel()
                {
                    Username = username,
                    Result = ConfirmOwnershipResult.NotYourRequest
                });
            }

            var package = _packageService.FindPackageRegistrationById(id);
            if (package == null)
            {
                return HttpNotFound();
            }

            var user = GetCurrentUser();
            ConfirmOwnershipResult result = _packageService.ConfirmPackageOwner(package, user, token);

            var model = new PackageOwnerConfirmationModel
                {
                    Result = result,
                    PackageId = package.Id
                };

            return View(model);
        }

        internal virtual ActionResult Edit(string id, string version, bool? listed, Func<Package, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            string action;
            if (!(listed ?? false))
            {
                action = "unlisted";
                _packageService.MarkPackageUnlisted(package);
            }
            else
            {
                action = "listed";
                _packageService.MarkPackageListed(package);
            }
            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture,
                "The package has been {0}. It may take several hours for this change to propagate through our system.",
                action);

            // Update the index
            _indexingService.UpdatePackage(package);
            return Redirect(urlFactory(package));
        }
        
        [Authorize]
        [RequiresAccountConfirmation("upload a package")]
        public virtual async Task<ActionResult> VerifyPackage()
        {
            var currentUser = GetCurrentUser();

            IPackageMetadata packageMetadata;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    return RedirectToRoute(RouteName.UploadPackage);
                }

                using (INupkg package = await SafeCreatePackage(currentUser, uploadFile))
                {
                    if (package == null)
                    {
                        return Redirect(Url.UploadPackage());
                    }
                    packageMetadata = package.Metadata;
                }
            }

            var model = new VerifyPackageRequest
            {
                Id = packageMetadata.Id,
                Version = packageMetadata.Version.ToNormalizedStringSafe(),
                LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull(),
                Listed = true,
                Edit = new EditPackageVersionRequest
                {
                    Authors = packageMetadata.Authors.Flatten(),
                    Copyright = packageMetadata.Copyright,
                    Description = packageMetadata.Description,
                    IconUrl = packageMetadata.IconUrl.ToEncodedUrlStringOrNull(),
                    LicenseUrl = packageMetadata.LicenseUrl.ToEncodedUrlStringOrNull(),
                    ProjectUrl = packageMetadata.ProjectUrl.ToEncodedUrlStringOrNull(),
                    ReleaseNotes = packageMetadata.ReleaseNotes,
                    RequiresLicenseAcceptance = packageMetadata.RequireLicenseAcceptance,
                    Summary = packageMetadata.Summary,
                    Tags = PackageHelper.ParseTags(packageMetadata.Tags),
                    VersionTitle = packageMetadata.Title,
                }
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [RequiresAccountConfirmation("upload a package")]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)] // Security note: Disabling ASP.Net input validation which does things like disallow angle brackets in submissions. See http://go.microsoft.com/fwlink/?LinkID=212874
        public virtual async Task<ActionResult> VerifyPackage(VerifyPackageRequest formData)
        {
            var currentUser = GetCurrentUser();

            Package package;
            using (Stream uploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (uploadFile == null)
                {
                    TempData["Message"] = "Your attempt to verify the package submission failed, because we could not find the uploaded package file. Please try again.";
                    return new RedirectResult(Url.UploadPackage());
                }

                INupkg nugetPackage = await SafeCreatePackage(currentUser, uploadFile);
                if (nugetPackage == null)
                {
                    // Send the user back
                    return new RedirectResult(Url.UploadPackage());
                }
                Debug.Assert(nugetPackage != null);

                // Rule out problem scenario with multiple tabs - verification request (possibly with edits) was submitted by user
                // viewing a different package to what was actually most recently uploaded
                if (!(String.IsNullOrEmpty(formData.Id) || String.IsNullOrEmpty(formData.Version)))
                {
                    if (!(String.Equals(nugetPackage.Metadata.Id, formData.Id, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(nugetPackage.Metadata.Version.ToNormalizedString(), formData.Version, StringComparison.OrdinalIgnoreCase)))
                    {
                        TempData["Message"] = "Your attempt to verify the package submission failed, because the package file appears to have changed. Please try again.";
                        return new RedirectResult(Url.VerifyPackage());
                    }
                }

                bool pendEdit = false;
                if (formData.Edit != null)
                {
                    pendEdit = pendEdit || formData.Edit.RequiresLicenseAcceptance != nugetPackage.Metadata.RequireLicenseAcceptance;

                    pendEdit = pendEdit || IsDifferent(formData.Edit.IconUrl, nugetPackage.Metadata.IconUrl.ToEncodedUrlStringOrNull());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ProjectUrl, nugetPackage.Metadata.ProjectUrl.ToEncodedUrlStringOrNull());

                    pendEdit = pendEdit || IsDifferent(formData.Edit.Authors, nugetPackage.Metadata.Authors.Flatten());
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Copyright, nugetPackage.Metadata.Copyright);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Description, nugetPackage.Metadata.Description);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.ReleaseNotes, nugetPackage.Metadata.ReleaseNotes);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Summary, nugetPackage.Metadata.Summary);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.Tags, nugetPackage.Metadata.Tags);
                    pendEdit = pendEdit || IsDifferent(formData.Edit.VersionTitle, nugetPackage.Metadata.Title);
                }

                // update relevant database tables
                package = _packageService.CreatePackage(nugetPackage, currentUser, commitChanges: false);
                Debug.Assert(package.PackageRegistration != null);

                _packageService.PublishPackage(package, commitChanges: false);

                if (pendEdit)
                {
                    // Add the edit request to a queue where it will be processed in the background.
                    _editPackageService.StartEditPackageRequest(package, formData.Edit, currentUser);
                }

                if (!formData.Listed)
                {
                    _packageService.MarkPackageUnlisted(package, commitChanges: false);
                }

                _autoCuratedPackageCmd.Execute(package, nugetPackage, commitChanges: false);

                // save package to blob storage
                uploadFile.Position = 0;
                await _packageFileService.SavePackageFileAsync(package, uploadFile);

                // commit all changes to database as an atomic transaction
                _entitiesContext.SaveChanges();

                // tell Lucene to update index for the new package
                _indexingService.UpdateIndex();
            }

            // delete the uploaded binary in the Uploads container
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture, Strings.SuccessfullyUploadedPackage, package.PackageRegistration.Id, package.Version);

            return RedirectToRoute(RouteName.DisplayPackage, new { package.PackageRegistration.Id, package.Version });
        }

        private async Task<INupkg> SafeCreatePackage(NuGetGallery.User currentUser, Stream uploadFile)
        {
            Exception caught = null;
            INupkg nugetPackage = null;
            try
            {
                nugetPackage = CreatePackage(uploadFile);
            }
            catch (InvalidPackageException ipex)
            {
                caught = ipex.AsUserSafeException();
            }
            catch (InvalidDataException idex)
            {
                caught = idex.AsUserSafeException();
            }
            catch (Exception ex)
            {
                // Can't wait for Roslyn to let us await in Catch blocks :(
                caught = ex;
            }
            if (caught != null)
            {
                caught.Log();
                // Report the error
                TempData["Message"] = caught.GetUserSafeMessage();

                // Clear the upload
                await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);
            }
            return nugetPackage;
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> CancelUpload()
        {
            var currentUser = GetCurrentUser();
            await _uploadFileService.DeleteUploadFileAsync(currentUser.Key);

            return RedirectToAction("UploadPackage");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual ActionResult SetLicenseReportVisibility(string id, string version, bool visible)
        {
            return SetLicenseReportVisibility(id, version, visible, Url.Package);
        }

        internal virtual ActionResult SetLicenseReportVisibility(string id, string version, bool visible, Func<Package, string> urlFactory)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);
            if (package == null)
            {
                return HttpNotFound();
            }
            if (!package.IsOwner(User))
            {
                return new HttpStatusCodeResult(401, "Unauthorized");
            }

            _packageService.SetLicenseReportVisibility(package, visible);

            TempData["Message"] = String.Format(
                CultureInfo.CurrentCulture,
                "The license report for this package has been {0}. It may take several hours for this change to propagate through our system.",
                visible ? "enabled" : "disabled");

            // Update the index
            _indexingService.UpdatePackage(package);

            return Redirect(urlFactory(package));
        }

        // this methods exist to make unit testing easier
        protected internal virtual INupkg CreatePackage(Stream stream)
        {
            try
            {
                return new Nupkg(stream, leaveOpen: false);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
        }

        private static string GetSortExpression(string sortOrder)
        {
            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    return "PackageRegistration.Id";
                case Constants.RecentSortOrder:
                    return "Published desc";

                default:
                    return "PackageRegistration.DownloadCount desc";
            }
        }

        // Determine whether an 'Edit' string submitted differs from one read from the package.
        private static bool IsDifferent(string posted, string package)
        {
            if (String.IsNullOrEmpty(posted) || String.IsNullOrEmpty(package))
            {
                return String.IsNullOrEmpty(posted) != String.IsNullOrEmpty(package);
            }

            // Compare non-empty strings
            // Ignore those pesky '\r' characters which screw up comparisons.
            return !String.Equals(posted.Replace("\r", ""), package.Replace("\r", ""), StringComparison.Ordinal);
        }
    }
}
