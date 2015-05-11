// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using Elmah;

namespace NuGetGallery
{
    internal static class QuietLog
    {
        public static void LogHandledException(Exception e)
        {
            try
            {
                ErrorSignal.FromCurrentContext().Raise(e);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}