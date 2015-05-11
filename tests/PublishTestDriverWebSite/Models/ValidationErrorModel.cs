// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PublishTestDriverWebSite.Models
{
    public class ValidationErrorModel
    {
        public ValidationErrorModel(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}