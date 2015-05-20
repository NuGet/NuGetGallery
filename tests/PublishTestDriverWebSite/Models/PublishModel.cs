// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace PublishTestDriverWebSite.Models
{
    public class PublishModel
    {
        public PublishModel()
        {
            Domains = new List<string>();
        }
        public IList<string> Domains { get; private set;  }
        public string Message { get; set; }
    }
}