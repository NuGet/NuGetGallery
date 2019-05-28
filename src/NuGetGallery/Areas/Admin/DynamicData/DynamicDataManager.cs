// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.DynamicData;
using System.Web.Routing;
using Microsoft.AspNet.DynamicData.ModelProviders;
using NuGet.Services.Sql;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class DynamicDataManager
    {
        public static MetaModel DefaultModel { get; } = new MetaModel { DynamicDataFolderVirtualPath = "~/Areas/Admin/DynamicData" };

        private static DynamicDataRoute _route;

        public static void Register(RouteCollection routes, string root, ISqlConnectionFactory galleryDbSqlConnectionFactory)
        {
            // Set up unobtrusive validation
            InitializeValidation();

            // Set up dynamic data
            InitializeDynamicData(routes, root, galleryDbSqlConnectionFactory);
        }

        private static void InitializeValidation()
        {
        }

        private static DbConnection CreateConnection(ISqlConnectionFactory connectionFactory)
        {
            return Task.Run(() => connectionFactory.CreateAsync()).Result;
        }

        private static void InitializeDynamicData(RouteCollection routes, string root, ISqlConnectionFactory connectionFactory)
        {
            try
            {
                DefaultModel.RegisterContext(
                    new EFDataModelProvider(
                        () => new EntitiesContext(CreateConnection(connectionFactory), readOnly: false)), // DB Admins do not need to respect read-only mode.
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
