using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SearchService
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(
                name: "Search",
                url: "search",
                defaults: new { controller = "Api", action = "Search" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") }
            );

            routes.MapRoute(
                name: "Range",
                url: "range",
                defaults: new { controller = "Api", action = "Range" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") }
            );

            routes.MapRoute(
                name: "Diag",
                url: "diag",
                defaults: new { controller = "Api", action = "Diag" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") }
            );

            routes.MapRoute(
                name: "Where",
                url: "where",
                defaults: new { controller = "Api", action = "Where" },
                constraints: new { httpMethod = new HttpMethodConstraint("GET") }
            );

            routes.MapRoute(
                name: "Segments",
                url: "segments",
                defaults: new { controller = "Home", action = "Segments", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "RangeQuery",
                url: "rangeQuery",
                defaults: new { controller = "Home", action = "RangeQuery", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}