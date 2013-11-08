using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    public class HelloWorldJob : Job
    {
        public string Message { get; set; }

        public override EventSource GetEventSource()
        {
            return HelloWorldEventSource.Log;
        }

        protected internal override Task Execute()
        {
            if (Message.Contains("you suck"))
            {
                HelloWorldEventSource.Log.TellingUserTheySuck();
                throw new Exception("NO YOU SUCK!");
            }
            HelloWorldEventSource.Log.Hello(Message);
            return Task.FromResult<object>(null);
        }

        [EventSource(Name = "NuGet-Jobs-HelloWorld")]
        public class HelloWorldEventSource : EventSource
        {
            public static readonly HelloWorldEventSource Log = new HelloWorldEventSource();

            private HelloWorldEventSource() { }

            [Event(eventId: 1, Message = "Hello world! Your message: {0}")]
            public void Hello(string message) { WriteEvent(1, message); }

            [Event(eventId: 2, Message = "The user said we suck! NO THEY SUCK!")]
            public void TellingUserTheySuck() { WriteEvent(2); }
        }
    }
}
