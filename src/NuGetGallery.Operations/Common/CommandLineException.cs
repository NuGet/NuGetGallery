// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace NuGetGallery.Operations
{
    [Serializable]
    public class CommandLineException : Exception
    {
        public CommandLineException()
        {
        }

        public CommandLineException(string message)
            : base(message)
        {
        }

        public CommandLineException(string format, params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, format, args))
        {
        }

        public CommandLineException(Exception innerException, string format, params object[] args)
            : base(String.Format(CultureInfo.CurrentCulture, format, args), innerException)
        {
        }

        public CommandLineException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected CommandLineException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
