﻿// <auto-generated />
// This file was generated by a T4 template.
// Don't change it directly as your change would get overwritten.  Instead, make changes
// to the .tt file (i.e. the T4 template) and save it to regenerate this file.

// Make sure the compiler doesn't complain about missing Xml comments
#pragma warning disable 1591
#region T4MVC

using System;
using System.Diagnostics;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Mvc.Html;
using System.Web.Routing;
using T4MVC;

[GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
public static class MVC {
    public static NuGetGallery.ApiController Api = new NuGetGallery.T4MVC_ApiController();
    public static NuGetGallery.AuthenticationController Authentication = new NuGetGallery.T4MVC_AuthenticationController();
    public static NuGetGallery.CuratedFeedsController CuratedFeeds = new NuGetGallery.T4MVC_CuratedFeedsController();
    public static NuGetGallery.CuratedPackagesController CuratedPackages = new NuGetGallery.T4MVC_CuratedPackagesController();
    public static NuGetGallery.JsonApiController JsonApi = new NuGetGallery.T4MVC_JsonApiController();
    public static NuGetGallery.PackageFilesController PackageFiles = new NuGetGallery.T4MVC_PackageFilesController();
    public static NuGetGallery.PackagesController Packages = new NuGetGallery.T4MVC_PackagesController();
    public static NuGetGallery.PagesController Pages = new NuGetGallery.T4MVC_PagesController();
    public static NuGetGallery.StatisticsController Statistics = new NuGetGallery.T4MVC_StatisticsController();
    public static NuGetGallery.UsersController Users = new NuGetGallery.T4MVC_UsersController();
    public static T4MVC.SharedController Shared = new T4MVC.SharedController();
}

namespace T4MVC {
}

   
namespace System.Web.Mvc {
    [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
    public static class T4Extensions {
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, string linkText, ActionResult result) {
            return htmlHelper.RouteLink(linkText, result.GetRouteValueDictionary());
        }

        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, string linkText, ActionResult result, object htmlAttributes) {
            return ActionLink(htmlHelper, linkText, result, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, string linkText, ActionResult result, object htmlAttributes, string protocol = null, string hostName = null, string fragment = null) {
            return ActionLink(htmlHelper, linkText, result, new RouteValueDictionary(htmlAttributes), protocol, hostName, fragment);
        }

        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, string linkText, ActionResult result, IDictionary<string, object> htmlAttributes, string protocol = null, string hostName = null, string fragment = null) {
            return htmlHelper.RouteLink(linkText, null, protocol, hostName, fragment, result.GetRouteValueDictionary(), htmlAttributes);
        }

        public static MvcForm BeginForm(this HtmlHelper htmlHelper, ActionResult result) {
            return htmlHelper.BeginForm(result, FormMethod.Post);
        }

        public static MvcForm BeginForm(this HtmlHelper htmlHelper, ActionResult result, FormMethod formMethod) {
            return htmlHelper.BeginForm(result, formMethod, null);
        }

        public static MvcForm BeginForm(this HtmlHelper htmlHelper, ActionResult result, FormMethod formMethod, object htmlAttributes) {
            return BeginForm(htmlHelper, result, formMethod, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcForm BeginForm(this HtmlHelper htmlHelper, ActionResult result, FormMethod formMethod, IDictionary<string, object> htmlAttributes) {
            var callInfo = result.GetT4MVCResult();
            return htmlHelper.BeginForm(callInfo.Action, callInfo.Controller, callInfo.RouteValueDictionary, formMethod, htmlAttributes);
        }

        public static void RenderAction(this HtmlHelper htmlHelper, ActionResult result) {
            var callInfo = result.GetT4MVCResult();
            htmlHelper.RenderAction(callInfo.Action, callInfo.Controller, callInfo.RouteValueDictionary);
        }

        public static MvcHtmlString Action(this HtmlHelper htmlHelper, ActionResult result) {
            var callInfo = result.GetT4MVCResult();
            return htmlHelper.Action(callInfo.Action, callInfo.Controller, callInfo.RouteValueDictionary);
        }
        public static string Action(this UrlHelper urlHelper, ActionResult result) {
            return urlHelper.RouteUrl(null, result.GetRouteValueDictionary());
        }

        public static string Action(this UrlHelper urlHelper, ActionResult result, string protocol = null, string hostName = null) {
            return urlHelper.RouteUrl(null, result.GetRouteValueDictionary(), protocol, hostName);
        }

        public static string ActionAbsolute(this UrlHelper urlHelper, ActionResult result) {
            return String.Format("{0}{1}",urlHelper.RequestContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority),
                urlHelper.RouteUrl(result.GetRouteValueDictionary()));
        }

        public static MvcHtmlString ActionLink(this AjaxHelper ajaxHelper, string linkText, ActionResult result, AjaxOptions ajaxOptions) {
            return ajaxHelper.RouteLink(linkText, result.GetRouteValueDictionary(), ajaxOptions);
        }

        public static MvcHtmlString ActionLink(this AjaxHelper ajaxHelper, string linkText, ActionResult result, AjaxOptions ajaxOptions, object htmlAttributes) {
            return ajaxHelper.RouteLink(linkText, result.GetRouteValueDictionary(), ajaxOptions, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcHtmlString ActionLink(this AjaxHelper ajaxHelper, string linkText, ActionResult result, AjaxOptions ajaxOptions, IDictionary<string, object> htmlAttributes) {
            return ajaxHelper.RouteLink(linkText, result.GetRouteValueDictionary(), ajaxOptions, htmlAttributes);
        }

        public static MvcForm BeginForm(this AjaxHelper ajaxHelper, ActionResult result, AjaxOptions ajaxOptions) {
            return ajaxHelper.BeginForm(result, ajaxOptions, null);
        }

        public static MvcForm BeginForm(this AjaxHelper ajaxHelper, ActionResult result, AjaxOptions ajaxOptions, object htmlAttributes) {
            return BeginForm(ajaxHelper, result, ajaxOptions, new RouteValueDictionary(htmlAttributes));
        }

        public static MvcForm BeginForm(this AjaxHelper ajaxHelper, ActionResult result, AjaxOptions ajaxOptions, IDictionary<string, object> htmlAttributes) {
            var callInfo = result.GetT4MVCResult();
            return ajaxHelper.BeginForm(callInfo.Action, callInfo.Controller, callInfo.RouteValueDictionary, ajaxOptions, htmlAttributes);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result) {
            return MapRoute(routes, name, url, result, null /*namespaces*/);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result, object defaults) {
            return MapRoute(routes, name, url, result, defaults, null /*constraints*/, null /*namespaces*/);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result, string[] namespaces) {
            return MapRoute(routes, name, url, result, null /*defaults*/, namespaces);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result, object defaults, object constraints) {
            return MapRoute(routes, name, url, result, defaults, constraints, null /*namespaces*/);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result, object defaults, string[] namespaces) {
            return MapRoute(routes, name, url, result, defaults, null /*constraints*/, namespaces);
        }

        public static Route MapRoute(this RouteCollection routes, string name, string url, ActionResult result, object defaults, object constraints, string[] namespaces) {
            // Create and add the route
            var route = CreateRoute(url, result, defaults, constraints, namespaces);
            routes.Add(name, route);
            return route;
        }

        // Note: can't name the AreaRegistrationContext methods 'MapRoute', as that conflicts with the existing methods
        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result) {
            return MapRouteArea(context, name, url, result, null /*namespaces*/);
        }

        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result, object defaults) {
            return MapRouteArea(context, name, url, result, defaults, null /*constraints*/, null /*namespaces*/);
        }

        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result, string[] namespaces) {
            return MapRouteArea(context, name, url, result, null /*defaults*/, namespaces);
        }

        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result, object defaults, object constraints) {
            return MapRouteArea(context, name, url, result, defaults, constraints, null /*namespaces*/);
        }

        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result, object defaults, string[] namespaces) {
            return MapRouteArea(context, name, url, result, defaults, null /*constraints*/, namespaces);
        }

        public static Route MapRouteArea(this AreaRegistrationContext context, string name, string url, ActionResult result, object defaults, object constraints, string[] namespaces) {
            // Create and add the route
            if ((namespaces == null) && (context.Namespaces != null)) {
                 namespaces = context.Namespaces.ToArray();
            }
            var route = CreateRoute(url, result, defaults, constraints, namespaces);
            context.Routes.Add(name, route);
            route.DataTokens["area"] = context.AreaName;
            bool useNamespaceFallback = (namespaces == null) || (namespaces.Length == 0);
            route.DataTokens["UseNamespaceFallback"] = useNamespaceFallback;
            return route;
        }

        private static Route CreateRoute(string url, ActionResult result, object defaults, object constraints, string[] namespaces) {
            // Start by adding the default values from the anonymous object (if any)
            var routeValues = new RouteValueDictionary(defaults);

            // Then add the Controller/Action names and the parameters from the call
            foreach (var pair in result.GetRouteValueDictionary()) {
                routeValues.Add(pair.Key, pair.Value);
            }

            var routeConstraints = new RouteValueDictionary(constraints);

            // Create and add the route
            var route = new Route(url, routeValues, routeConstraints, new MvcRouteHandler());

            route.DataTokens = new RouteValueDictionary();

            if (namespaces != null && namespaces.Length > 0) {
                route.DataTokens["Namespaces"] = namespaces;
            }

            return route;
        }

        public static IT4MVCActionResult GetT4MVCResult(this ActionResult result) {
            var t4MVCResult = result as IT4MVCActionResult;
            if (t4MVCResult == null) {
                throw new InvalidOperationException("T4MVC was called incorrectly. You may need to force it to regenerate by right clicking on T4MVC.tt and choosing Run Custom Tool");
            }
            return t4MVCResult;
        }

        public static RouteValueDictionary GetRouteValueDictionary(this ActionResult result) {
            return result.GetT4MVCResult().RouteValueDictionary;
        }

        public static ActionResult AddRouteValues(this ActionResult result, object routeValues) {
            return result.AddRouteValues(new RouteValueDictionary(routeValues));
        }

        public static ActionResult AddRouteValues(this ActionResult result, RouteValueDictionary routeValues) {
            RouteValueDictionary currentRouteValues = result.GetRouteValueDictionary();

            // Add all the extra values
            foreach (var pair in routeValues) {
                currentRouteValues.Add(pair.Key, pair.Value);
            }

            return result;
        }

        public static ActionResult AddRouteValues(this ActionResult result, System.Collections.Specialized.NameValueCollection nameValueCollection) {
            // Copy all the values from the NameValueCollection into the route dictionary
            nameValueCollection.CopyTo(result.GetRouteValueDictionary());
            return result;
        }

        public static ActionResult AddRouteValue(this ActionResult result, string name, object value) {
            RouteValueDictionary routeValues = result.GetRouteValueDictionary();
            routeValues.Add(name, value);
            return result;
        }
        
        public static void InitMVCT4Result(this IT4MVCActionResult result, string area, string controller, string action) {
            result.Controller = controller;
            result.Action = action;
            result.RouteValueDictionary = new RouteValueDictionary();
            if (!String.IsNullOrWhiteSpace(area)) {result.RouteValueDictionary.Add("Area", area ?? "");} 
            result.RouteValueDictionary.Add("Controller", controller);
            result.RouteValueDictionary.Add("Action", action);
        }

        public static bool FileExists(string virtualPath) {
            if (!HostingEnvironment.IsHosted) return false;
            string filePath = HostingEnvironment.MapPath(virtualPath);
            return System.IO.File.Exists(filePath);
        }

        static DateTime CenturyBegin=new DateTime(2001,1,1);
        public static string TimestampString(string virtualPath) {
            if (!HostingEnvironment.IsHosted) return String.Empty;
            string filePath = HostingEnvironment.MapPath(virtualPath);
            return Convert.ToString((System.IO.File.GetLastWriteTimeUtc(filePath).Ticks-CenturyBegin.Ticks)/1000000000,16);            
        }
    }
}



namespace T4MVC {
    [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
    public class Dummy {
        private Dummy() { }
        public static Dummy Instance = new Dummy();
    }
}


  

   
[GeneratedCode("T4MVC", "2.0")]   
public interface IT4MVCActionResult {   
    string Action { get; set; }   
    string Controller { get; set; }   
    RouteValueDictionary RouteValueDictionary { get; set; }   
}   
  

[GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
public class T4MVC_ActionResult : System.Web.Mvc.ActionResult, IT4MVCActionResult {
    public T4MVC_ActionResult(string area, string controller, string action): base()  {
        this.InitMVCT4Result(area, controller, action);
    }
     
    public override void ExecuteResult(System.Web.Mvc.ControllerContext context) { }
    
    public string Controller { get; set; }
    public string Action { get; set; }
    public RouteValueDictionary RouteValueDictionary { get; set; }
}
[GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
public class T4MVC_JsonResult : System.Web.Mvc.JsonResult, IT4MVCActionResult {
    public T4MVC_JsonResult(string area, string controller, string action): base()  {
        this.InitMVCT4Result(area, controller, action);
    }
    
    public string Controller { get; set; }
    public string Action { get; set; }
    public RouteValueDictionary RouteValueDictionary { get; set; }
}



namespace Links {
    [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
    public static class Scripts {
        private const string URLPATH = "~/Scripts";
        public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
        public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
        public static readonly string async_file_upload_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/async-file-upload.min.js") ? Url("async-file-upload.min.js") : Url("async-file-upload.js");
                      
        public static readonly string jquery_1_6_2_vsdoc_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/jquery-1.6.2-vsdoc.min.js") ? Url("jquery-1.6.2-vsdoc.min.js") : Url("jquery-1.6.2-vsdoc.js");
                      
        public static readonly string jquery_1_6_2_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/jquery-1.6.2.min.js") ? Url("jquery-1.6.2.min.js") : Url("jquery-1.6.2.js");
                      
        public static readonly string jquery_1_6_2_min_js = Url("jquery-1.6.2.min.js");
        public static readonly string jquery_treeview_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/jquery.treeview.min.js") ? Url("jquery.treeview.min.js") : Url("jquery.treeview.js");
                      
        public static readonly string jquery_validate_vsdoc_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/jquery.validate-vsdoc.min.js") ? Url("jquery.validate-vsdoc.min.js") : Url("jquery.validate-vsdoc.js");
                      
        public static readonly string jquery_validate_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/jquery.validate.min.js") ? Url("jquery.validate.min.js") : Url("jquery.validate.js");
                      
        public static readonly string jquery_validate_min_js = Url("jquery.validate.min.js");
        public static readonly string jquery_validate_unobtrusive_min_js = Url("jquery.validate.unobtrusive.min.js");
        public static readonly string knockout_latest_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/knockout-latest.min.js") ? Url("knockout-latest.min.js") : Url("knockout-latest.js");
                      
        public static readonly string modernizr_2_0_6_development_only_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/modernizr-2.0.6-development-only.min.js") ? Url("modernizr-2.0.6-development-only.min.js") : Url("modernizr-2.0.6-development-only.js");
                      
        public static readonly string stats_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/stats.min.js") ? Url("stats.min.js") : Url("stats.js");
                      
        [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
        public static class SyntaxHighlighter {
            private const string URLPATH = "~/Scripts/SyntaxHighlighter";
            public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
            public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
            public static readonly string shAutoloader_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shAutoloader.min.js") ? Url("shAutoloader.min.js") : Url("shAutoloader.js");
                          
            public static readonly string shBrushAppleScript_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushAppleScript.min.js") ? Url("shBrushAppleScript.min.js") : Url("shBrushAppleScript.js");
                          
            public static readonly string shBrushAS3_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushAS3.min.js") ? Url("shBrushAS3.min.js") : Url("shBrushAS3.js");
                          
            public static readonly string shBrushBash_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushBash.min.js") ? Url("shBrushBash.min.js") : Url("shBrushBash.js");
                          
            public static readonly string shBrushColdFusion_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushColdFusion.min.js") ? Url("shBrushColdFusion.min.js") : Url("shBrushColdFusion.js");
                          
            public static readonly string shBrushCpp_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushCpp.min.js") ? Url("shBrushCpp.min.js") : Url("shBrushCpp.js");
                          
            public static readonly string shBrushCSharp_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushCSharp.min.js") ? Url("shBrushCSharp.min.js") : Url("shBrushCSharp.js");
                          
            public static readonly string shBrushCss_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushCss.min.js") ? Url("shBrushCss.min.js") : Url("shBrushCss.js");
                          
            public static readonly string shBrushDelphi_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushDelphi.min.js") ? Url("shBrushDelphi.min.js") : Url("shBrushDelphi.js");
                          
            public static readonly string shBrushDiff_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushDiff.min.js") ? Url("shBrushDiff.min.js") : Url("shBrushDiff.js");
                          
            public static readonly string shBrushErlang_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushErlang.min.js") ? Url("shBrushErlang.min.js") : Url("shBrushErlang.js");
                          
            public static readonly string shBrushGroovy_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushGroovy.min.js") ? Url("shBrushGroovy.min.js") : Url("shBrushGroovy.js");
                          
            public static readonly string shBrushJava_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushJava.min.js") ? Url("shBrushJava.min.js") : Url("shBrushJava.js");
                          
            public static readonly string shBrushJavaFX_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushJavaFX.min.js") ? Url("shBrushJavaFX.min.js") : Url("shBrushJavaFX.js");
                          
            public static readonly string shBrushJScript_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushJScript.min.js") ? Url("shBrushJScript.min.js") : Url("shBrushJScript.js");
                          
            public static readonly string shBrushPerl_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushPerl.min.js") ? Url("shBrushPerl.min.js") : Url("shBrushPerl.js");
                          
            public static readonly string shBrushPhp_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushPhp.min.js") ? Url("shBrushPhp.min.js") : Url("shBrushPhp.js");
                          
            public static readonly string shBrushPlain_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushPlain.min.js") ? Url("shBrushPlain.min.js") : Url("shBrushPlain.js");
                          
            public static readonly string shBrushPowerShell_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushPowerShell.min.js") ? Url("shBrushPowerShell.min.js") : Url("shBrushPowerShell.js");
                          
            public static readonly string shBrushPython_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushPython.min.js") ? Url("shBrushPython.min.js") : Url("shBrushPython.js");
                          
            public static readonly string shBrushRuby_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushRuby.min.js") ? Url("shBrushRuby.min.js") : Url("shBrushRuby.js");
                          
            public static readonly string shBrushSass_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushSass.min.js") ? Url("shBrushSass.min.js") : Url("shBrushSass.js");
                          
            public static readonly string shBrushScala_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushScala.min.js") ? Url("shBrushScala.min.js") : Url("shBrushScala.js");
                          
            public static readonly string shBrushSql_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushSql.min.js") ? Url("shBrushSql.min.js") : Url("shBrushSql.js");
                          
            public static readonly string shBrushVb_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushVb.min.js") ? Url("shBrushVb.min.js") : Url("shBrushVb.js");
                          
            public static readonly string shBrushXml_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shBrushXml.min.js") ? Url("shBrushXml.min.js") : Url("shBrushXml.js");
                          
            public static readonly string shCore_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shCore.min.js") ? Url("shCore.min.js") : Url("shCore.js");
                          
            public static readonly string shLegacy_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/shLegacy.min.js") ? Url("shLegacy.min.js") : Url("shLegacy.js");
                          
        }
    
        public static readonly string ZeroClipboard_js = T4MVCHelpers.IsProduction() && T4Extensions.FileExists(URLPATH + "/ZeroClipboard.min.js") ? Url("ZeroClipboard.min.js") : Url("ZeroClipboard.js");
                      
        public static readonly string ZeroClipboard_swf = Url("ZeroClipboard.swf");
    }

    [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
    public static class Content {
        private const string URLPATH = "~/Content";
        public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
        public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
        [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
        public static class Images {
            private const string URLPATH = "~/Content/Images";
            public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
            public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
            public static readonly string changePassword_png = Url("changePassword.png");
            public static readonly string copy_png = Url("copy.png");
            public static readonly string download_png = Url("download.png");
            public static readonly string editIcon_png = Url("editIcon.png");
            public static readonly string editProfile_png = Url("editProfile.png");
            public static readonly string errorPage_png = Url("errorPage.png");
            public static readonly string greenArrow_png = Url("greenArrow.png");
            public static readonly string headerbackground_png = Url("headerbackground.png");
            public static readonly string hero_png = Url("hero.png");
            public static readonly string herowithlogo_png = Url("herowithlogo.png");
            public static readonly string inputBackground_png = Url("inputBackground.png");
            public static readonly string invalidBG_png = Url("invalidBG.png");
            public static readonly string managePackages_png = Url("managePackages.png");
            public static readonly string mine_png = Url("mine.png");
            public static readonly string navbackground_png = Url("navbackground.png");
            public static readonly string newAccountGraphic_png = Url("newAccountGraphic.png");
            public static readonly string nugetlogo_png = Url("nugetlogo.png");
            public static readonly string nugetLogoFooter_png = Url("nugetLogoFooter.png");
            public static readonly string packageDefaultIcon_50x50_png = Url("packageDefaultIcon-50x50.png");
            public static readonly string packageDefaultIcon_png = Url("packageDefaultIcon.png");
            public static readonly string packageOwnerActionIcons_png = Url("packageOwnerActionIcons.png");
            public static readonly string packagesDefaultIcon_png = Url("packagesDefaultIcon.png");
            public static readonly string recommended_png = Url("recommended.png");
            public static readonly string recommendedSmall_png = Url("recommendedSmall.png");
            public static readonly string required_png = Url("required.png");
            public static readonly string searchButton_png = Url("searchButton.png");
            public static readonly string sendMessageGraphic_png = Url("sendMessageGraphic.png");
            public static readonly string trash_png = Url("trash.png");
            [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
            public static class treeview {
                private const string URLPATH = "~/Content/Images/treeview";
                public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
                public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
                public static readonly string ajax_loader_gif = Url("ajax-loader.gif");
                public static readonly string file_gif = Url("file.gif");
                public static readonly string folder_closed_gif = Url("folder-closed.gif");
                public static readonly string folder_gif = Url("folder.gif");
                public static readonly string minus_gif = Url("minus.gif");
                public static readonly string plus_gif = Url("plus.gif");
                public static readonly string treeview_black_line_gif = Url("treeview-black-line.gif");
                public static readonly string treeview_black_gif = Url("treeview-black.gif");
                public static readonly string treeview_default_line_gif = Url("treeview-default-line.gif");
                public static readonly string treeview_default_gif = Url("treeview-default.gif");
                public static readonly string treeview_famfamfam_line_gif = Url("treeview-famfamfam-line.gif");
                public static readonly string treeview_famfamfam_gif = Url("treeview-famfamfam.gif");
                public static readonly string treeview_gray_line_gif = Url("treeview-gray-line.gif");
                public static readonly string treeview_gray_gif = Url("treeview-gray.gif");
                public static readonly string treeview_red_line_gif = Url("treeview-red-line.gif");
                public static readonly string treeview_red_gif = Url("treeview-red.gif");
            }
        
            public static readonly string uploadPackage_png = Url("uploadPackage.png");
            public static readonly string userGraphic_png = Url("userGraphic.png");
            public static readonly string userIcon_png = Url("userIcon.png");
            public static readonly string userIconWhite_png = Url("userIconWhite.png");
            public static readonly string xmark_png = Url("xmark.png");
            public static readonly string YourPackage_png = Url("YourPackage.png");
        }
    
        public static readonly string jquery_treeview_css = Url("jquery.treeview.css");
        public static readonly string Site_css = Url("Site.css");
        [GeneratedCode("T4MVC", "2.0"), DebuggerNonUserCode]
        public static class SyntaxHighlighter {
            private const string URLPATH = "~/Content/SyntaxHighlighter";
            public static string Url() { return T4MVCHelpers.ProcessVirtualPath(URLPATH); }
            public static string Url(string fileName) { return T4MVCHelpers.ProcessVirtualPath(URLPATH + "/" + fileName); }
            public static readonly string shCore_css = Url("shCore.css");
            public static readonly string shCoreDefault_css = Url("shCoreDefault.css");
            public static readonly string shCoreDjango_css = Url("shCoreDjango.css");
            public static readonly string shCoreEclipse_css = Url("shCoreEclipse.css");
            public static readonly string shCoreEmacs_css = Url("shCoreEmacs.css");
            public static readonly string shCoreFadeToGrey_css = Url("shCoreFadeToGrey.css");
            public static readonly string shCoreMDUltra_css = Url("shCoreMDUltra.css");
            public static readonly string shCoreMidnight_css = Url("shCoreMidnight.css");
            public static readonly string shCoreRDark_css = Url("shCoreRDark.css");
            public static readonly string shThemeDefault_css = Url("shThemeDefault.css");
            public static readonly string shThemeDjango_css = Url("shThemeDjango.css");
            public static readonly string shThemeEclipse_css = Url("shThemeEclipse.css");
            public static readonly string shThemeEmacs_css = Url("shThemeEmacs.css");
            public static readonly string shThemeFadeToGrey_css = Url("shThemeFadeToGrey.css");
            public static readonly string shThemeMDUltra_css = Url("shThemeMDUltra.css");
            public static readonly string shThemeMidnight_css = Url("shThemeMidnight.css");
            public static readonly string shThemeRDark_css = Url("shThemeRDark.css");
        }
    
    }

}

static class T4MVCHelpers {
    // You can change the ProcessVirtualPath method to modify the path that gets returned to the client.
    // e.g. you can prepend a domain, or append a query string:
    //      return "http://localhost" + path + "?foo=bar";
    private static string ProcessVirtualPathDefault(string virtualPath) {
        // The path that comes in starts with ~/ and must first be made absolute
        string path = VirtualPathUtility.ToAbsolute(virtualPath);
        
        // Add your own modifications here before returning the path
        return path;
    }

    // Calling ProcessVirtualPath through delegate to allow it to be replaced for unit testing
    public static Func<string, string> ProcessVirtualPath = ProcessVirtualPathDefault;


    // Logic to determine if the app is running in production or dev environment
    public static bool IsProduction() { 
        return (HttpContext.Current != null && !HttpContext.Current.IsDebuggingEnabled); 
    }
}





#endregion T4MVC
#pragma warning restore 1591


