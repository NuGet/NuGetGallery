// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace NuGetGallery.Operations
{
    public static class LoggerExtensions
    {
        public static void Http(this Logger self, HttpWebResponse response)
        {
            string message = String.Format(
                CultureInfo.CurrentCulture,
                "http {0} {1}",
                (int)response.StatusCode,
                response.ResponseUri.AbsoluteUri);
            if ((int)response.StatusCode >= 400)
            {
                self.Error(message);
            }
            else
            {
                self.Info(message);
            }
        }

        public static void Http(this Logger self, HttpWebRequest request)
        {
            self.Info("http {0} {1}", request.Method, request.RequestUri.AbsoluteUri);
        }
    }
}