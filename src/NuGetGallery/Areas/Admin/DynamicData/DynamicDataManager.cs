// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.DynamicData.ModelProviders;
using NuGetGallery.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Web.DynamicData;
using System.Web.Routing;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class DynamicDataManager
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "We do treat this as immutable.")]
        public static readonly MetaModel DefaultModel = new MetaModel { DynamicDataFolderVirtualPath = "~/Areas/Admin/DynamicData" };

        private static DynamicDataRoute _route;

        public static void Register(RouteCollection routes, string root, IGalleryConfigurationService configService)
        {
            // Set up unobtrusive validation
            InitializeValidation();

            // Set up dynamic data
            InitializeDynamicData(routes, root, configService);
        }

        private static void InitializeValidation()
        {
        }
        
        private static void InitializeDynamicData(RouteCollection routes, string root, IGalleryConfigurationService configService)
        {
            try
            {
                DefaultModel.RegisterContext(
                    new EFDataModelProvider(
                        () => new EntitiesContext(configService.Current.SqlConnectionString, readOnly: false)), // DB Admins do not need to respect read-only mode.
                        configuration: new ContextConfiguration { ScaffoldAllTables = true });
            }
            catch (SqlException e)
            {
                QuietLog.LogHandledException(e);
                return;
            }
            catch (DataException e)
            {
                QuietLog.LogHandledException(e);
                return;
            }

            // This route must come first to prevent some other route from the site to take over
            _route = new DynamicDataRoute(root + "/{table}/{action}")
            {
                Constraints = new RouteValueDictionary(new { action = "List|Details|Edit|Insert" }),
                Model = DefaultModel
            };
            routes.Insert(0, _route);

            routes.MapPageRoute(
                "dd_default",
                root,
                "~/Areas/Admin/DynamicData/Default.aspx");
        }
    }
}
