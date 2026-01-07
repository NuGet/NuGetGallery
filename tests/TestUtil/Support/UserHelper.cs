// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Principal;
using Xunit;

namespace TestUtil
{
    public static class UserHelper
    {
        /// <summary>
        /// Certain tests fail when run as a non-Administrator user. Adding the environment
        /// variable with the name specified below and any value will enable skipping those
        /// tests when run as non-Administrator. They will still run if executed as Admin.
        /// </summary>
        public const string EnableSkipVariableName = "ENABLE_NONADMIN_TEST_SKIP";

        /// <summary>
        /// Source: https://stackoverflow.com/a/11660205
        /// </summary>
        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void SetupFactSkipIfAdmin(FactAttribute attribute)
        {
            if (!UserHelper.IsAdministrator() && Environment.GetEnvironmentVariable(EnableSkipVariableName) != null)
            {
                attribute.Skip = "Test will not run unless executed as Administrator";
            }
        }
    }
}
