using System;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class TaskFactoryWrapper : ITaskFactory
    {
        readonly TaskFactory _taskFactory;
        
        public TaskFactoryWrapper(TaskFactory taskFactory)
        {
            _taskFactory = taskFactory;
        }

        public Task StartNew(Action action)
        {
            return _taskFactory.StartNew(action);
        }
    }
}