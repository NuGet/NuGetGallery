// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGetGallery
{
    /// <summary>
    /// An exception to be used for Elmah logs.
    /// </summary>
    public class ElmahException : Exception
    {
        private Exception _baseException;

        /// <summary>
        /// Server variables values in Elmah logs will be overwritten by these values.
        /// </summary>
        public Dictionary<string, string> ServerVariables
        {
            get;
        }

        public ElmahException(Exception e, Dictionary<string, string> serverVariables) : base(e.Message, e.InnerException)
        {
            ServerVariables = serverVariables ?? new Dictionary<string, string>();
            _baseException = e;
        }

        public override string StackTrace => _baseException.StackTrace;

        public override IDictionary Data => _baseException.Data;

        public override string Source { get => _baseException.Source; set => Source = value; }

        public override Exception GetBaseException()
        {
            return _baseException.GetBaseException();
        }

        public override string HelpLink { get => _baseException.HelpLink; set => HelpLink = value; }

        public override string ToString()
        {
            return _baseException.ToString();
        }
    }
}