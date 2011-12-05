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
    [System.Web.WebPages.PageVirtualPathAttribute("~/Views/Users/ResendConfirmation.cshtml")]
    public class ResendConfirmation : System.Web.Mvc.WebViewPage<ResendConfirmationEmailViewModel>
    {
        public ResendConfirmation()
        {
        }
        public override void Execute()
        {


            
            #line 2 "..\..\Views\Users\ResendConfirmation.cshtml"
  
    ViewBag.Title = "Resend Email Confirmation";


            
            #line default
            #line hidden
WriteLiteral(@"
<h1 class=""page-heading"">Resend Email Confirmation</h1>

<p>
     We are sorry to hear you did not get our confirmation email. Enter your email below 
     and we will resend the confirmation email. Alternatively you might have been 
     redirected to this page because the confirmation link you clicked was old and need 
     to be updated. Enter the email address and a new one will be send, sorry for the 
     inconvenience. 
</p>

");


            
            #line 16 "..\..\Views\Users\ResendConfirmation.cshtml"
 using (Html.BeginForm()) {

            
            #line default
            #line hidden
WriteLiteral("    <fieldset class=\"form\">\n        <legend>Resend Email Confirmation</legend>\n\n " +
"       ");


            
            #line 20 "..\..\Views\Users\ResendConfirmation.cshtml"
   Write(Html.AntiForgeryToken());

            
            #line default
            #line hidden
WriteLiteral("\n        ");


            
            #line 21 "..\..\Views\Users\ResendConfirmation.cshtml"
   Write(Html.ValidationSummary(true));

            
            #line default
            #line hidden
WriteLiteral("\n              \n        ");


            
            #line 23 "..\..\Views\Users\ResendConfirmation.cshtml"
   Write(Html.EditorForModel());

            
            #line default
            #line hidden
WriteLiteral("\n\n        <img src=\"");


            
            #line 25 "..\..\Views\Users\ResendConfirmation.cshtml"
             Write(Url.Content("~/content/images/required.png"));

            
            #line default
            #line hidden
WriteLiteral("\" alt=\"Blue border on left means required.\" />\n\n        <input type=\"submit\" valu" +
"e=\"Send\" title=\"Resend Email Confirmation\" />\n        <a class=\"cancel\" href=\"");


            
            #line 28 "..\..\Views\Users\ResendConfirmation.cshtml"
                           Write(Url.LogOn());

            
            #line default
            #line hidden
WriteLiteral("\" title=\"Cancel Changes and go back.\">Cancel</a>\n    </fieldset>\n");


            
            #line 30 "..\..\Views\Users\ResendConfirmation.cshtml"
}

            
            #line default
            #line hidden
WriteLiteral("\n");


DefineSection("BottomScripts", () => {

WriteLiteral("\n    <script src=\"");


            
            #line 33 "..\..\Views\Users\ResendConfirmation.cshtml"
            Write(Url.Content("~/Scripts/jquery.validate.min.js"));

            
            #line default
            #line hidden
WriteLiteral("\" type=\"text/javascript\"></script>\n    <script src=\"");


            
            #line 34 "..\..\Views\Users\ResendConfirmation.cshtml"
            Write(Url.Content("~/Scripts/jquery.validate.unobtrusive.min.js"));

            
            #line default
            #line hidden
WriteLiteral("\" type=\"text/javascript\"></script>\n");


});


        }
    }
}
#pragma warning restore 1591
