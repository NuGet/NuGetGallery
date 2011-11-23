﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.239
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGetGallery.Views.Packages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Web;
    using System.Web.Helpers;
    using System.Web.Mvc;
    using System.Web.Mvc.Ajax;
    using System.Web.Mvc.Html;
    using System.Web.Routing;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.WebPages;
    using Microsoft.Web.Helpers;
    using NuGetGallery;
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "1.2.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/Views/Packages/ListPackages.cshtml")]
    public class ListPackages : System.Web.Mvc.WebViewPage<PackageListViewModel>
    {
        public ListPackages()
        {
        }
        public override void Execute()
        {


            
            #line 2 "..\..\Views\Packages\ListPackages.cshtml"
  
    ViewBag.Tab = "Packages";


            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n\r\n<hgroup class=\"search\">\r\n");


            
            #line 9 "..\..\Views\Packages\ListPackages.cshtml"
     if (!String.IsNullOrEmpty(Model.SearchTerm)) {

            
            #line default
            #line hidden
WriteLiteral("    <h1>Search for \"");


            
            #line 10 "..\..\Views\Packages\ListPackages.cshtml"
               Write(Model.SearchTerm);

            
            #line default
            #line hidden
WriteLiteral("\" returned ");


            
            #line 10 "..\..\Views\Packages\ListPackages.cshtml"
                                           Write(Model.TotalCount);

            
            #line default
            #line hidden
WriteLiteral(" packages</h1>\r\n");


            
            #line 11 "..\..\Views\Packages\ListPackages.cshtml"
    }
    else {

            
            #line default
            #line hidden
WriteLiteral("    <h1>There are ");


            
            #line 13 "..\..\Views\Packages\ListPackages.cshtml"
             Write(Model.TotalCount);

            
            #line default
            #line hidden
WriteLiteral(" packages</h1>\r\n");


            
            #line 14 "..\..\Views\Packages\ListPackages.cshtml"
    }

            
            #line default
            #line hidden
WriteLiteral("    <h2>Displaying results ");


            
            #line 15 "..\..\Views\Packages\ListPackages.cshtml"
                      Write(Model.FirstResultIndex);

            
            #line default
            #line hidden
WriteLiteral(" - ");


            
            #line 15 "..\..\Views\Packages\ListPackages.cshtml"
                                                Write(Model.LastResultIndex);

            
            #line default
            #line hidden
WriteLiteral(".</h2>\r\n</hgroup>\r\n\r\n");


            
            #line 18 "..\..\Views\Packages\ListPackages.cshtml"
 using (Html.BeginForm()) {

            
            #line default
            #line hidden
WriteLiteral("    <fieldset class=\"form search\">\r\n        <legend>Sort Order</legend>\r\n        " +
"<input type=\"hidden\" name=\"q\" value=\"");


            
            #line 21 "..\..\Views\Packages\ListPackages.cshtml"
                                        Write(Model.SearchTerm);

            
            #line default
            #line hidden
WriteLiteral("\" />\r\n        <div class=\"form-field\">\r\n            <label for=\"sortOrder\">Sort B" +
"y</label>\r\n            <select name=\"sortOrder\" id=\"sortOrder\">\r\n               " +
" ");


            
            #line 25 "..\..\Views\Packages\ListPackages.cshtml"
           Write(ViewHelpers.Option("package-title", "A-Z", Model.SortOrder));

            
            #line default
            #line hidden
WriteLiteral("\r\n                ");


            
            #line 26 "..\..\Views\Packages\ListPackages.cshtml"
           Write(ViewHelpers.Option("package-download-count", "Popularity", Model.SortOrder));

            
            #line default
            #line hidden
WriteLiteral("\r\n                ");


            
            #line 27 "..\..\Views\Packages\ListPackages.cshtml"
           Write(ViewHelpers.Option("package-created", "Recent", Model.SortOrder));

            
            #line default
            #line hidden
WriteLiteral("\r\n            </select>\r\n        </div>\r\n    </fieldset>\r\n");


            
            #line 31 "..\..\Views\Packages\ListPackages.cshtml"
}

            
            #line default
            #line hidden
WriteLiteral("\r\n<ol id=\"searchResults\">\r\n");


            
            #line 34 "..\..\Views\Packages\ListPackages.cshtml"
     foreach (var package in Model.Items) {

            
            #line default
            #line hidden
WriteLiteral("    <li>\r\n        ");


            
            #line 36 "..\..\Views\Packages\ListPackages.cshtml"
   Write(Html.Partial(MVC.Packages.Views._ListPackage, package));

            
            #line default
            #line hidden
WriteLiteral("\r\n    </li>\r\n");


            
            #line 38 "..\..\Views\Packages\ListPackages.cshtml"
    }

            
            #line default
            #line hidden
WriteLiteral("</ol>\r\n\r\n\r\n");


            
            #line 42 "..\..\Views\Packages\ListPackages.cshtml"
Write(ViewHelpers.PreviousNextPager(Model.Pager));

            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n");


DefineSection("BottomScripts", () => {

WriteLiteral("\r\n    <script>\r\n        $(function () {\r\n            $(\"#sortOrder\").change(funct" +
"ion () {\r\n                $(this).closest(\"form\").submit();\r\n            });\r\n  " +
"      });\r\n    </script>\r\n");


});

WriteLiteral("\r\n\r\n");


        }
    }
}
#pragma warning restore 1591
