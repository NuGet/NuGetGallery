using System;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class JsonNetResult : ActionResult
    {
	    private readonly object _data;

	    public JsonNetResult(object data)
	    {
		    if (data == null)
			    throw new ArgumentNullException("data");
			
		    _data = data;
	    }

	    public override void ExecuteResult(ControllerContext context)
	    {
		    if (context == null)
			    throw new ArgumentNullException("context");

		    var response = context.HttpContext.Response;
		    response.ContentType = "application/json";
		    var writer = new JsonTextWriter(response.Output);
		    var serializer = JsonSerializer.Create(new JsonSerializerSettings());
		    serializer.Serialize(writer, _data);
		    writer.Flush();
	    }
    }
}