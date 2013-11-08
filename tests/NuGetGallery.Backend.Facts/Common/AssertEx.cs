using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xunit
{
    /// <summary>
    /// Extra asserters
    /// </summary>
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
