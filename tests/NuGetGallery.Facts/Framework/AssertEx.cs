using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Framework
{
    public static class AssertEx
    {
        public static async Task<TException> Throws<TException>(Func<Task> action)
            where TException : Exception
        {
            TException captured = null;
            try
            {
                await action();
            }
            catch (TException ex)
            {
                captured = ex;
            }

            Assert.NotNull(captured);
            return captured;
        }
    }
}
