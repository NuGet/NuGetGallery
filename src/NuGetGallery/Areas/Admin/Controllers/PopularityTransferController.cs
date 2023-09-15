using Azure.Core;
using Lucene.Net.Search;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Auditing;
using NuGet.Services.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class PopularityTransferController : AdminControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly IEntityRepository<PackageRename> _packageRenameRepository;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAuditingService _auditingService;
        private readonly ITelemetryService _telemetryService;

        public PopularityTransferController(
            IPackageService packageService,
            ITelemetryService telemetryService,
            IEntityRepository<PackageRename> packageRenameRepository,
            IEntitiesContext entitiesContext,
            IDateTimeProvider dateTimeProvider,
            IAuditingService auditingService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageRenameRepository = packageRenameRepository ?? throw new ArgumentNullException(nameof(packageRenameRepository));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        [HttpGet]
        public ViewResult Index(PopularityTransferViewModel viewModel)
        {
            return View(viewModel ?? new PopularityTransferViewModel());
        }

        [HttpGet]
        public ActionResult ValidateInputs(string packagesFromInput, string packagesToInput)
        {
            if (string.IsNullOrEmpty(packagesFromInput) || string.IsNullOrEmpty(packagesToInput))
            {
                return Json(HttpStatusCode.BadRequest, "Package IDs in the 'From' or 'To' fields cannot be null or empty.", JsonRequestBehavior.AllowGet);
            }

            var packagesFromListTemp = packagesFromInput
                                                .Split(null) // all whitespace
                                                .ToList();
            var packagesToListTemp = packagesToInput
                                                .Split(null) // all whitespace
                                                .ToList();

            var packagesFromList = packagesFromInput
                                                .Split(null) // all whitespace
                                                .Select(id => _packageService.FindPackageRegistrationById(id))
                                                .ToList();
            var packagesToList = packagesToInput
                                                .Split(null) // all whitespace
                                                .Select(id => _packageService.FindPackageRegistrationById(id))
                                                .ToList();

            if (packagesFromList.Count != packagesToList.Count)
            {
                return Json(HttpStatusCode.BadRequest, "There must be an equal number of Package IDs in the 'From' and 'To' fields.", JsonRequestBehavior.AllowGet);
            }

            var resultTemp = new ValidatedInputsResult();

            for (int i = 0; i < packagesFromList.Count; i++)
            {
                var input = new ValidatedInput(packagesFromListTemp[i], packagesToListTemp[i]);
                resultTemp.ValidatedInputs.Add(input);
            }

            var result = new ValidatedInputsResult();

            for (int i = 0; i < packagesFromList.Count; i++)
            {
                var input = new ValidatedInput(CreatePackageSearchResult(packagesFromList[i].Packages.First()),
                                               CreatePackageSearchResult(packagesToList[i].Packages.First()));

                result.ValidatedInputs.Add(input);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}