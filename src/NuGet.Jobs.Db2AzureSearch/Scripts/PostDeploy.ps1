.\RunJob.cmd

if ($LastExitCode -ne 0) {
    throw "The job failed with exit code $LastExitCode"
}
