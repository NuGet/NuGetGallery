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
    [System.Web.WebPages.PageVirtualPathAttribute("~/Views/Packages/Edit.cshtml")]
    public class Edit : System.Web.Mvc.WebViewPage<dynamic>
    {
        public Edit()
        {
        }
        public override void Execute()
        {

            
            #line 1 "..\..\Views\Packages\Edit.cshtml"
  
    ViewBag.Tab = "Packages";


            
            #line default
            #line hidden
WriteLiteral("\r\n<h1 class=\"page-heading\">Edit ");


            
            #line 5 "..\..\Views\Packages\Edit.cshtml"
                         Write(Model.Title);

            
            #line default
            #line hidden
WriteLiteral(" Package</h1>\r\n<p class=\"message\">\r\n    To edit the metadata for a package, pleas" +
"e <a href=\"");


            
            #line 7 "..\..\Views\Packages\Edit.cshtml"
                                                   Write(Url.UploadPackage());

            
            #line default
            #line hidden
WriteLiteral(@""">upload an updated version of the package</a>.
</p>
<p>
    NuGet currently does not allow updating package metadata on the website. This helps ensure 
    that the package itself (and the source used to build the package) remains the one true 
    source of package metadata.
</p>
<p>
    This does require that you increment the package version.
</p>
");


        }
    }
}
#pragma warning restore 1591
