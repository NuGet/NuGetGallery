using System;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class JsonNetResult : ActionResult
    {
	    public JsonNetResult(object data)
	    {
		    if (data == null)
			    throw new ArgumentNullException("data");
			
		    Data = data;
	    }

        public object Data { get; private set; }

	    public override void ExecuteResult(ControllerContext context)
	    {
		    if (context == null)
			    throw new ArgumentNullException("context");

		    var response = context.HttpContext.Response;
		    response.ContentType = "application/json";
		    var writer = new JsonTextWriter(response.Output);
		    var serializer = JsonSerializer.Create(new JsonSerializerSettings());
		    serializer.Serialize(writer, Data);
		    writer.Flush();
	    }
    }
}