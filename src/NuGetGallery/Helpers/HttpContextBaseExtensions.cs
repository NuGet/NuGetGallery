﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Text;
using System.Web;
using System.Web.Security;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public static class HttpContextBaseExtensions
    {
        public static void SetConfirmationReturnUrl(this HttpContextBase httpContext, string returnUrl)
        {
            var confirmationContext = new ConfirmationContext
            {
                ReturnUrl = returnUrl,
            };
            string json = JsonConvert.SerializeObject(confirmationContext);
            string protectedJson = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(json), "ConfirmationContext"));
            HttpCookie responseCookie = new HttpCookie("ConfirmationContext", protectedJson);
            responseCookie.HttpOnly = true;
            httpContext.Response.Cookies.Add(responseCookie);
        }

        public static string GetConfirmationReturnUrl(this HttpContextBase httpContext)
        {
            HttpCookie cookie = null;
            if (httpContext.Request.Cookies != null)
            {
                cookie = httpContext.Request.Cookies.Get("ConfirmationContext");
            }

            if (cookie == null)
            {
                return null;
            }

            var protectedJson = cookie.Value;
            if (String.IsNullOrEmpty(protectedJson))
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(protectedJson), "ConfirmationContext"));
            var confirmationContext = JsonConvert.DeserializeObject<ConfirmationContext>(json);
            return confirmationContext.ReturnUrl;
        }
    }

    public class ConfirmationContext
    {
        public string ReturnUrl { get; set; }
    }
}