// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

namespace PublishTestDriverWebSite.Models
{
    public class ServiceErrorModel
    {
        Exception _e;
        HttpStatusCode _statusCode;
        string _message;
        
        public ServiceErrorModel(Exception e)
        {
            _e = e;
        }

        public ServiceErrorModel(HttpStatusCode statusCode, string message)
        {
            _statusCode = statusCode;
            _message = message;
        }

        public bool IsException
        {
            get { return _e != null; }
        }

        public string Exception
        {
            get { return _e.GetType().Name + " " + _e.Message; }
        }

        public string StatusCode
        {
            get { return _statusCode.ToString(); }
        }

        public string Message
        {
            get { return _message; }
        }
    }
}