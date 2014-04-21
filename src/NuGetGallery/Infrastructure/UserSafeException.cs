using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    // Marker interface to indicate that the exception has a message that can be shown to a user
    public interface IUserSafeException
    {
        string UserMessage { get; }
        Exception LoggedException { get; }
    }

    [Serializable]
    public class UserSafeException : Exception, IUserSafeException
    {
        public string UserMessage
        {
            get { return Message; }
        }

        public Exception LoggedException
        {
            get { return InnerException == null ? this : InnerException; }
        }

        public UserSafeException() { }
        public UserSafeException(string message) : base(message) { }
        public UserSafeException(string message, Exception inner) : base(message, inner) { }
        protected UserSafeException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    public static class UserSafeExceptionExtensions
    {
        public static UserSafeException AsUserSafeException(this Exception self)
        {
            return new UserSafeException(self.Message, self.InnerException);
        }

        public static string GetUserSafeMessage(this Exception self)
        {
            IUserSafeException uvex = self as IUserSafeException;
            if (uvex != null)
            {
                return uvex.UserMessage;
            }
            return Strings.DefaultUserSafeExceptionMessage;
        }

        public static void Log(this Exception self)
        {
            IUserSafeException uvex = self as IUserSafeException;
            if (uvex != null)
            {
                // Log the exception that the User-Visible wrapper marked as to-be-logged
                QuietLog.LogHandledException(uvex.LoggedException);
            }
            else
            {
                QuietLog.LogHandledException(self);
            }
        }
    }
}