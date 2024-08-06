// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class ImageResult : ActionResult
    {
        public ImageResult(Stream imageStream, string contentType)
        {
            if (imageStream == null)
            {
                throw new ArgumentNullException(nameof(imageStream));
            }

            if (contentType == null)
            {
                throw new ArgumentNullException(nameof(contentType));
            }

            ImageStream = imageStream;
            ContentType = contentType;
        }

        public Stream ImageStream { get; private set; }
        public string ContentType { get; private set; }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            HttpResponseBase response = context.HttpContext.Response;
            response.ContentType = ContentType;
            
            ImageStream.CopyTo(response.OutputStream);
            response.End();
        }
    }
}
