using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend
{
    internal static class RunnerId
    {
        private const string RunnerIdDataName = "_NuGet_Backend_Runner_Id";

        public static int Get()
        {
            return (int)CallContext.LogicalGetData(RunnerIdDataName);
        }

        public static void Set(int id)
        {
            CallContext.LogicalSetData(RunnerIdDataName, id);
        }
    }
}
