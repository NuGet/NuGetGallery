// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public class DownloadCountRecord
    {
        public int Downloads { get; set; }
        public int RegistrationDownloads { get; set; }
        public int Installs { get; set; }
        public int Updates { get; set; }
    }
}
