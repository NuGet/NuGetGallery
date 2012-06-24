using System;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ITaskFactory
    {
        Task StartNew(Action action);
    }
}