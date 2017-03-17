// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Ng
{
    /// <summary>
    /// A TransientException for cases when the job's execution loop can continue without the process to be shutdown. 
    /// </summary>
    [Serializable] 
    public class TransientException : System.Exception
    {
        public TransientException(string message ) : base(message)
        {
        }

        public TransientException(string message, Exception innerException) : base(message, innerException) 
        {
        }

        protected TransientException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    /// <summary>
    /// Transient exception for http client timeout. 
    /// </summary>
    [Serializable]
    public class HttpClientTimeoutException : TransientException
    {
        public HttpClientTimeoutException(string message) : base(message)
        {
        }

        public HttpClientTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HttpClientTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
