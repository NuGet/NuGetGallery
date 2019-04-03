// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseMigrationTools
{
    class Program
    {
        public static int Main(string[] args)
        {
            DatabaseMigrationService.UpdateDatabase();
            return 1;
        }
    }
}
