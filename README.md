NuGet.WebJobs
==============

1. Each job would be an exe with 2 main classes Program and Job
2. Program.Main should simply do the following and nothing more
    *var job = new Job();
    JobRunner.Run(job, args).Wait();*
3. Job class must inherit NuGet.Jobs.Common.JobBase and implement abstract methods Init and Run
4. An IDictionary<string, string> is passed to Init for the job to initialize the member variables
5. Edit the project file on a new job and always set <DefineConstants>TRACE</DefineConstants> irrespective of configuration
