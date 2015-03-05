
using System.Collections.Generic;
namespace PublishTestDriverWebSite.Models
{
    public class UploadModel
    {
        public UploadModel()
        {
            IsSuccess = true;
        }

        public UploadModel(string error)
        {
            Error = error;
            IsSuccess = false;
        }

        public UploadModel(IEnumerable<string> errors)
        {
            Errors = errors;
            IsSuccess = false;
        }

        public bool IsSuccess { get; private set; }

        public string Error { get; private set; }
        public IEnumerable<string> Errors { get; private set; }
    }
}