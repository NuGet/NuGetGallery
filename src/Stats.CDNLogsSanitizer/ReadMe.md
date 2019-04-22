
# Summary

Stats.CDNLogsSanitizer is a tool that can be used for cleaning or transforming the data in CDN logs. The tool loads CDN logs form an Azure storage, transforms them and saves the transformed version to a destination Azure storage.
It is intended to be executed manually.

# Settings description
- **AzureAccountConnectionStringSource** 
  - The connection string of the source of the cdn logs to be transformed.
- **AzureAccountConnectionStringDestination**
  - The connection string of the destination of the cdn logs to be transformed.
- **AzureContainerNameSource**
  -  The container name of the CDN source logs.
- **AzureContainerNameDestination**
  -  The container name of the CDN destination logs. If the container does not exists it will be created.
- **BlobPrefix**
  -  If not all the blobs from the input storage need to be proccesed the blob prefix can be used to filter them. Let it empty if a filter is not needed.
- **LogHeader**
  - The CDN logs can have different header formats. Sample `"LogHeader": "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip"`
- **LogHeaderDelimiter**
  - Some CDN logs can be comma separated but others can be space separated. 
- **ExecutionTimeoutInSeconds**
  - After this number of seconds the execution will be cancelled. 
- **MaxBlobsToProcess**
  - The blobs are processed in parallel. This is the max number of blobs to be processed at once. 

# Execution command
`Stats.CDNLogsSanitizer.exe -Configuration "Path to a json file like Settings\chinadev.json." -InstrumentationKey "An intrumentation key for log traces." -verbose true `