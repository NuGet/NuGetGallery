using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Backend.Tracing;

namespace NuGetGallery.Backend.Jobs
{
    public class HelloWorldJob : Job
    {
        public string Message { get; set; }

        public override JobEventSource BaseLog { get { return HelloWorldJobEventSource.Log; } }

        protected internal override void Execute()
        {
            if (Message.Contains("you suck"))
            {
                throw new Exception("No YOU suck!");
            }
            HelloWorldJobEventSource.Log.HelloWorld(Message);
        }

        public class HelloWorldJobEventSource : JobEventSource<HelloWorldJob>
        {
            public static readonly HelloWorldJobEventSource Log = new HelloWorldJobEventSource();

            private HelloWorldJobEventSource() : base() { }

            [Event(
                eventId: BaseId + 0,
                Level = EventLevel.Informational,
                Message = "Hello World! Your Message: {0}")]
            public void HelloWorld(string message) { WriteEvent(BaseId + 0, message); }
        }
    }
}
