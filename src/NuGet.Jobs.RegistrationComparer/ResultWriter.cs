// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Jobs.RegistrationComparer
{
    public static class ResultWriter
    {
        private static readonly object _warningsLock = new object();
        private static readonly object _errorsLock = new object();

        public static void WriteWarning(string message)
        {
            lock (_warningsLock)
            {
                File.AppendAllLines("warnings.txt", new[] { message.Trim(), Environment.NewLine });
            }
        }

        public static void WriteError(string message)
        {
            lock (_errorsLock)
            {
                File.AppendAllLines("errors.txt", new[] { message.Trim(), Environment.NewLine });
            }
        }
    }
}
