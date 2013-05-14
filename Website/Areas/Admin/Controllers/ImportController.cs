using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.WebPages;

using NuGet;

using NuGetGallery.AsyncFileUpload;
using NuGetGallery.NuGetService;

namespace NuGetGallery.Areas.Admin.Controllers
{
    /// <summary>
    /// Controller for importing directly from the NuGet official package source.
    /// </summary>
    public partial class ImportController : AdminControllerBase
    {
        private FeedContext_x0060_1 _nugetFeedContext;

        private readonly IConfiguration _config;

        private readonly ICacheService _cacheService;

        private readonly IUploadFileService _uploadFileService;

        private readonly IUserService _userService;

        private readonly IPackageService _packageService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportController"/> class.
        /// </summary>
        /// <param name="uploadFileService">The upload file service.</param>
        /// <param name="userService">The user service.</param>
        /// <param name="packageService">The package service.</param>
        /// <param name="config">The config.</param>
        /// <param name="cacheService">The cache service.</param>
        public ImportController(IUploadFileService uploadFileService, IUserService userService, IPackageService packageService, IConfiguration config, ICacheService cacheService)
        {
            _packageService = packageService;
            _userService = userService;
            _uploadFileService = uploadFileService;
            _cacheService = cacheService;
            _config = config;
            var nugetOfficialUrl = ConfigurationManager.AppSettings["Gallery.OfficialNuGetUrl"];
            _nugetFeedContext = new FeedContext_x0060_1(new Uri(nugetOfficialUrl));
        }

        /// <summary>
        /// Displays the details of a package to be imported.
        /// </summary>
        /// <param name="id">The package id.</param>
        /// <param name="version">The package version.</param>
        /// <returns>Page with details for package.</returns>
        public virtual ActionResult Details(string id, string version)
        {
            var packages = _nugetFeedContext.Packages.Where(p => p.Id == id)
                            .OrderByDescending(p => p.Version);
            NuGetService.V2FeedPackage package = null;
            
            if (!string.IsNullOrWhiteSpace(version))
            {
                package = packages.Where(p => p.Version == version).FirstOrDefault();
            }
            else
            {
                package = packages.FirstOrDefault();
            }
            
            if (package == null)
            {
                return HttpNotFound();
            }

            // Get all existing versions that have already been imported.
            var existingPackage = _packageService.FindPackageByIdAndVersion(id, version);
            var importedVersions = existingPackage != null ? existingPackage.PackageRegistration.Packages.Select(p => p.Version) : Enumerable.Empty<string>();

            var model = new ImportPackageViewModel(package, packages, importedVersions);
            ViewBag.FacebookAppID = _config.FacebookAppID;
            return View(model);
        }

        /// <summary>
        /// Searches for a package to import from the official NuGet feed.
        /// </summary>
        /// <param name="q">The text to search for.</param>
        /// <param name="sortOrder">The sort order.</param>
        /// <param name="page">The page.</param>
        /// <param name="prerelease">if set to <c>true</c> show pre-release versions.</param>
        /// <returns>Search results.</returns>
        public virtual ActionResult Search(string q, string sortOrder = null, int page = 1, bool prerelease = false)
        {
            if (page < 1)
            {
                page = 1;
            }

            q = (q ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(sortOrder))
            {
                // Determine the default sort order. If no query string is specified, then the sortOrder is DownloadCount
                // If we are searching for something, sort by relevance.
                sortOrder = q.IsEmpty() ? Constants.PopularitySortOrder : Constants.RelevanceSortOrder;
            }

            // Get all packages with matching title or tags.
            IQueryable<NuGetService.V2FeedPackage> packages = from p in _nugetFeedContext.Packages
                                                 where p.IsLatestVersion && (p.Title.Contains(q) || p.Tags.Contains(q))
                                                 select p;

            // Show pre-release if specified.
            if (!prerelease)
            {
                packages = packages.Where(p => !p.IsPrerelease);
            }

            // Set requested sort order.
            switch (sortOrder)
            {
                case Constants.AlphabeticSortOrder:
                    packages = packages.OrderBy(p => p.Title);
                    break;

                case Constants.RecentSortOrder:
                    packages = packages.OrderByDescending(p => p.LastUpdated);
                    break;

                case Constants.PopularitySortOrder:
                    packages = packages.OrderByDescending(p => p.DownloadCount);
                    break;

                default:    // Relevance
                    packages = packages.OrderByDescending(p => p.Title.Contains(q)).ThenByDescending(p => p.DownloadCount);
                    break;
            }

            int totalHits = packages.Count();
            var viewModel = new ImportSearchViewModel(
                packages,
                q,
                sortOrder,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url,
                prerelease);

            ViewBag.SearchTerm = q;

            return View(viewModel);
        }

        /// <summary>
        /// Displays a page to download and import from the official NuGet feed.
        /// </summary>
        /// <param name="importModel">The import package model.</param>
        /// <param name="ignore">Ignore this parameter.</param>
        /// <returns>
        /// Page to confirm import.
        /// </returns>
        public virtual ActionResult Download(ImportDownloadViewModel importModel, string ignore)
        {
            return View(importModel);
        }

        /// <summary>
        /// Downloads the specified package from the NuGet official source.
        /// </summary>
        /// <param name="importModel">The import package model.</param>
        /// <returns>
        /// Details page on failure; otherwise verification page.
        /// </returns>
        [HttpPost]
        public async Task<ActionResult> Download(ImportDownloadViewModel importModel)
        {
            var currentUser = _userService.FindByUsername(GetIdentity().Name);

            using (var existingUploadFile = await _uploadFileService.GetUploadFileAsync(currentUser.Key))
            {
                if (existingUploadFile != null)
                {
                    ModelState.AddModelError(string.Empty, "Cannot upload file because an upload is already in progress.");
                    return View(importModel);
                }
            }

            if (string.IsNullOrWhiteSpace(importModel.Id))
            {
                ModelState.AddModelError("id", "Import package ID is required.");
                return View(importModel);
            }

            if (string.IsNullOrWhiteSpace(importModel.Version))
            {
                ModelState.AddModelError("version", "Import package version is required.");
                return View(importModel);
            }

            using (var uploadStream = new MemoryStream())
            {
                INupkg nuGetPackage;
                try
                {
                    // Download package from official NuGet source.
                    string downloadUrl = string.Format("{0}/package/{1}/{2}", ConfigurationManager.AppSettings["Gallery.OfficialNuGetUrl"], importModel.Id, importModel.Version);
                    var httpClient = new HttpClient(new Uri(downloadUrl));
                    httpClient.DownloadData(uploadStream);
                    nuGetPackage = CreatePackage(uploadStream);
                }
                catch
                {
                    ModelState.AddModelError(String.Empty, Strings.FailedToReadUploadFile);
                    return View(importModel);
                }
                finally
                {
                    _cacheService.RemoveProgress(currentUser.Username);
                }

                var packageRegistration = _packageService.FindPackageRegistrationById(nuGetPackage.Metadata.Id);
                if (packageRegistration != null && !packageRegistration.Owners.AnySafe(x => x.Key == currentUser.Key))
                {
                    ModelState.AddModelError(
                        String.Empty, String.Format(CultureInfo.CurrentCulture, Strings.PackageIdNotAvailable, packageRegistration.Id));
                    return View(importModel);
                }

                var package = _packageService.FindPackageByIdAndVersion(nuGetPackage.Metadata.Id, nuGetPackage.Metadata.Version.ToStringSafe());
                if (package != null)
                {
                    ModelState.AddModelError(
                        String.Empty,
                        String.Format(
                            CultureInfo.CurrentCulture, Strings.PackageExistsAndCannotBeModified, package.PackageRegistration.Id, package.Version));
                    return View(importModel);
                }

                await _uploadFileService.SaveUploadFileAsync(currentUser.Key, nuGetPackage.GetStream());
            }

            return RedirectToRoute(RouteName.VerifyPackage);            
        }

        // this methods exist to make unit testing easier
        protected internal virtual IIdentity GetIdentity()
        {
            return User.Identity;
        }

        // this methods exist to make unit testing easier
        protected internal virtual INupkg CreatePackage(Stream stream)
        {
            return new Nupkg(stream, leaveOpen: false);
        }
    }
}
