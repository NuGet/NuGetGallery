﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.237
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGetGallery.Views.Users
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
    [System.Web.WebPages.PageVirtualPathAttribute("~/Views/Users/PasswordSent.cshtml")]
    public class PasswordSent : System.Web.Mvc.WebViewPage<dynamic>
    {
        public PasswordSent()
        {
        }
        public override void Execute()
        {

            
            #line 1 "..\..\Views\Users\PasswordSent.cshtml"
  
    ViewBag.Title = "Password Reset";


            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n<h1 class=\"page-heading\">Password Reset Sent</h1>\r\n\r\n\r\n<p>\r\n    We\'ve sent an" +
" email \r\n");


            
            #line 11 "..\..\Views\Users\PasswordSent.cshtml"
     if (!String.IsNullOrEmpty(ViewBag.Email)) {

            
            #line default
            #line hidden
WriteLiteral("        ");

WriteLiteral("to ");


            
            #line 12 "..\..\Views\Users\PasswordSent.cshtml"
        Write(ViewBag.Email);

            
            #line default
            #line hidden
WriteLiteral("\r\n");


            
            #line 13 "..\..\Views\Users\PasswordSent.cshtml"
    } 
    else {

            
            #line default
            #line hidden
WriteLiteral("        ");

WriteLiteral("to you\r\n");


            
            #line 16 "..\..\Views\Users\PasswordSent.cshtml"
    } 

            
            #line default
            #line hidden
WriteLiteral("    containing a temporary url that will allow you to reset your password for \r\n " +
"   the next ");


            
            #line 18 "..\..\Views\Users\PasswordSent.cshtml"
        Write(ViewBag.Expiration);

            
            #line default
            #line hidden
WriteLiteral(" hours. \r\n</p>\r\n<p>\r\n    Please check your spam folder if you don\'t receive the e" +
"mail within a \r\n    few minutes.\r\n</p>");


        }
    }
}
#pragma warning restore 1591
