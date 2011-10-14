using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using MvcMiniProfiler;
using MvcMiniProfiler.MVCHelpers;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGetGallery.App_Start.MiniProfilerPackage), "PreStart")]
[assembly: WebActivator.PostApplicationStartMethod(typeof(NuGetGallery.App_Start.MiniProfilerPackage), "PostStart")]

namespace NuGetGallery.App_Start
{
    public static class MiniProfilerPackage
    {
        public static void PreStart()
        {
            MiniProfiler.Settings.SqlFormatter = new MvcMiniProfiler.SqlFormatters.SqlServerFormatter();

            var sqlConnectionFactory = new SqlConnectionFactory(ConfigurationManager.ConnectionStrings["NuGetGallery"].ConnectionString);
            var profiledConnectionFactory = new MvcMiniProfiler.Data.ProfiledDbConnectionFactory(sqlConnectionFactory);
            Database.DefaultConnectionFactory = profiledConnectionFactory;

            DynamicModuleUtility.RegisterModule(typeof(MiniProfilerStartupModule));
            GlobalFilters.Filters.Add(new ProfilingActionFilter());
        }

        public static void PostStart()
        {
            var viewEngines = ViewEngines.Engines.ToList();

            ViewEngines.Engines.Clear();

            foreach (var item in viewEngines)
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
        }
    }

    public class MiniProfilerStartupModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
            {
                var request = ((HttpApplication)sender).Request;

                MiniProfiler.Start();
            };

            context.AuthorizeRequest += (sender, e) =>
            {
                if (HttpContext.Current == null || HttpContext.Current.User == null || !HttpContext.Current.User.IsInRole(Const.AdminRoleName))
                    MiniProfiler.Stop(true);
            };

            context.EndRequest += (sender, e) =>
            {
                MiniProfiler.Stop();
            };
        }

        public void Dispose()
        {
        }
    }
}