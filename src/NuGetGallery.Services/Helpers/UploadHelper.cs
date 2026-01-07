// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Specialized;

namespace NuGetGallery.Helpers
{
    public static class UploadHelper
    {
        public static string GetUploadTracingKey(NameValueCollection headers)
        {
            if (headers == null)
            {
                return Guid.Empty.ToString();
            }

            string uploadTracingKey;
            try
            {
                uploadTracingKey = headers[CoreConstants.UploadTracingKeyHeaderName];
                uploadTracingKey = Guid.Parse(uploadTracingKey).ToString();
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentNullException)
            {
                // An upload tracing key was not found
                // Simultaneous UI uploads might have strange behaviour.
                // Note that we might have this case if an old client sends to new Server.
                uploadTracingKey = Guid.Empty.ToString();
            }

            return uploadTracingKey;
        }
    }
}