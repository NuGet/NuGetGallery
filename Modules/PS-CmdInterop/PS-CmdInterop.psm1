<#
	.Synopsis
		Invokes the specified CMD-shell script and imports it's environment
	.Description
		Invoking a CMD-shell script from PowerShell normally causes a new CMD.exe
		process to be created and the script run within it. The problem with this
		is that the environment variables set by the script are lost when this process
		terminates and control is returned to PowerShell. This cmdlet copies the 
		environment variables over to the PowerShell environment.
	.Parameter Script
		The script to run
	.Parameter Parameters
		The parameters to pass to the script
	.Example
		Invoke-CmdScript ScriptWhichSetsVars.cmd /a /b /c
	.Inputs
		System.String
	.Outputs
		Nothing
#>	
function Invoke-CmdScript {
	# Adapted from http://www.leeholmes.com/blog/2006/05/11/nothing-solves-everything-%E2%80%93-powershell-and-other-technologies/
	param(
		[Parameter(Mandatory=$true, ValueFromPipeline=$true)][string]$Script,
		[Parameter(Mandatory=$false, ValueFromRemainingArguments=$true)][string]$Parameters
	)
	
	$tempFile = [System.IO.Path]::GetTempFileName()
	
	cmd /c " `"$Script`" $Parameters && set > `"$tempFile`" "
	
	Get-Content $tempFile | ForEach-Object {
			if($_ -match "^(?<var>.*?)=(?<val>.*)$") {
				Set-Content "env:\$($matches['var'])" $matches['val']
			}
	}
	
	Remove-Item $tempFile
}
Export-ModuleMember -Function Invoke-CmdScript