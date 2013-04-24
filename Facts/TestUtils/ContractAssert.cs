using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery
{
    public static class ContractAssert
    {
        public static TException Throws<TException>(Action act, string message) where TException : Exception
        {
            var ex = Assert.Throws<TException>(() => act());
            Assert.Equal(message, ex.Message);
            return ex;
        }

        public static void ThrowsArgNull(Action act, string paramName)
        {
            var argNullEx = Assert.Throws<ArgumentNullException>(() => act());
            Assert.Equal(paramName, argNullEx.ParamName);
        }

        public static void ThrowsOutOfRange(Action act, string baseMessage, string paramName, object actualValue)
        {
            var argEx = ThrowsArgException<ArgumentOutOfRangeException>(
                act,
                paramName,
                FormatArgumentExceptionMessage(paramName, String.Format(baseMessage, paramName)) + 
                    Environment.NewLine + 
                    "Actual value was " + actualValue.ToString() + ".");
            Assert.Equal(actualValue, argEx.ActualValue);
        }

        public static void ThrowsArgNullOrEmpty(Action<string> act, string paramName)
        {
            var message = String.Format(Strings.ParameterCannotBeNullOrEmpty, paramName);
            ContractAssert.ThrowsArgException(() => act(null), paramName, message);
            ContractAssert.ThrowsArgException(() => act(String.Empty), paramName, message);
        }

        public static TArgEx ThrowsArgException<TArgEx>(Action act, string paramName, string message) where TArgEx : ArgumentException
        {
            var argEx = Assert.Throws<TArgEx>(() => act());
            Assert.Equal(paramName, argEx.ParamName);
            Assert.Equal(
                message,
                argEx.Message);
            return argEx;
        }

        public static ArgumentException ThrowsArgException(Action act, string paramName, string message)
        {
            return ThrowsArgException<ArgumentException>(act, paramName, FormatArgumentExceptionMessage(paramName, message));
        }

        private static string FormatArgumentExceptionMessage(string paramName, string message)
        {
            return message + Environment.NewLine + String.Format("Parameter name: {0}", paramName);
        }
    }
}
