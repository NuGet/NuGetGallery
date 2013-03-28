using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.DynamicData;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Data;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class MigrationsController : AdminControllerBase
    {
        public IDatabaseVersioningService DatabaseVersioning { get; protected set; }
        public IDbModelManager ModelManager { get; protected set; }
        public IEntitiesContextFactory ContextFactory { get; protected set; }

        protected MigrationsController()
        {
        }

        public MigrationsController(IDatabaseVersioningService databaseVersioning, IDbModelManager modelManager, IEntitiesContextFactory contextFactory)
        {
            DatabaseVersioning = databaseVersioning;
            ModelManager = modelManager;
            ContextFactory = contextFactory;
        }

        //
        // GET: /Admin/Migrations/

        public virtual ActionResult Index()
        {
            var model = new MigrationListViewModel()
            {
                Applied = DatabaseVersioning.AppliedVersions
                    .Select(id => CreateMigrationViewModel(id))
                    .ToList(),
                Available = DatabaseVersioning.AvailableVersions
                    .Select(id => CreateMigrationViewModel(id))
                    .ToList(),
                Pending = DatabaseVersioning.PendingVersions
                    .Select(id => CreateMigrationViewModel(id))
                    .ToList()
            };
            return View(model);
        }

        [HttpPost]
        public virtual ActionResult Apply()
        {
            DatabaseVersioning.UpdateToLatest();
            ModelManager.RebuildModel();
            return RedirectToAction(MVC.Admin.Migrations.Index());
        }

        private MigrationViewModel CreateMigrationViewModel(string id)
        {
            DatabaseVersion version = DatabaseVersioning.GetVersion(id);
            return new MigrationViewModel()
            {
                Id = version.Id,
                CreatedUtc = version.CreatedUtc,
                Name = version.Name,
                Description = version.Description
            };
        }
    }

}
