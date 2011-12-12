using System;
using System.IO;
using System.Web.Mvc;
using Amazon.S3;
using Amazon.S3.Model;

namespace NuGetGallery
{
    public class AmazonS3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3Client clientContext;

        public AmazonS3FileStorageService(IAmazonS3Client clientContext)
        {
            this.clientContext = clientContext;
        }

        public ActionResult CreateDownloadFileActionResult(string folderName, string fileName)
        {
            //folder ignored - packages stored in top level of S3 bucket
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");

            var downloadLink = BuildPath(fileName);

            return new RedirectResult(downloadLink, false);
        }

        public string BuildPath(string fileName)
        {
            //string.IsNullOrEmpty(folderName) ? String.Empty : folderName + "/",
            return string.Format("http://{0}.s3.amazonaws.com/{1}", clientContext.BucketName, fileName);
        }

        private T WrapRequestInErrorHandler<T>(Func<T> func)
        {
            try
            {
                return func.Invoke();
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null && (
                    amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                    || amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    throw new AmazonS3Exception(
                        "Please check the provided AWS Credentials. If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3",
                        amazonS3Exception);
                }

                throw;
            }
        }

        public void DeleteFile(string folderName, string fileName)
        {
            //folder ignored - packages stored on top level of S3 bucket
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");

            DeleteObjectRequest request = new DeleteObjectRequest();
            request.WithBucketName(clientContext.BucketName);
            request.WithKey(fileName);

            using (AmazonS3 client = clientContext.CreateInstance())
            {
                S3Response response = WrapRequestInErrorHandler(() => client.DeleteObject(request));
            }
        }

        public Stream GetFile(string folderName, string fileName)
        {
            //folder ignored - packages stored on top level of S3 bucket
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");

            GetObjectRequest request = new GetObjectRequest();
            request.WithBucketName(clientContext.BucketName);
            request.WithKey(fileName);
            request.WithTimeout((int)TimeSpan.FromMinutes(30).TotalMilliseconds);

            using (AmazonS3 client = this.clientContext.CreateInstance())
            {
                try
                {
                    S3Response response = WrapRequestInErrorHandler(() => client.GetObject(request));

                    if (response != null)
                        return response.ResponseStream;
                }
                catch (Exception)
                {
                    //hate swallowing an error
                }

                return null;
            }
        }

        public void SaveFile(string folderName, string fileName, Stream fileStream)
        {
            //folder ignored - packages stored on top level of S3 bucket
            if (String.IsNullOrWhiteSpace(folderName))
                throw new ArgumentNullException("folderName");
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException("fileName");
            if (fileStream == null)
                throw new ArgumentNullException("fileStream");


            PutObjectRequest request = new PutObjectRequest();
            request.WithBucketName(clientContext.BucketName);
            request.WithKey(fileName);
            request.WithInputStream(fileStream);
            request.AutoCloseStream = true;
            request.CannedACL = S3CannedACL.PublicRead;
            request.WithTimeout((int)TimeSpan.FromMinutes(30).TotalMilliseconds);

            using (AmazonS3 client = this.clientContext.CreateInstance())
            {
                S3Response response = WrapRequestInErrorHandler(() => client.PutObject(request));
            }
        }
    }
}