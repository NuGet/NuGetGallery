function require-param {
  param($value, $paramName)
  
  if ($value -eq $null) {
    write-error "The parameter -$paramName is required."
    exit 1
  }
}

function print-error {
    param($value)
    Write-Host $value -foregroundcolor White -backgroundcolor Red
}

function print-warning {
    param($value)
    Write-Host $value -foregroundcolor Red -backgroundcolor Yellow
}

function print-success {
    param($value)
    Write-Host $value -foregroundcolor Black -backgroundcolor Green
}

function print-message {
    param($value)
    Write-Host $value -foregroundcolor Black -backgroundcolor White
}

function Get-StorageAccountConnectionString {
    param($name)

    $StorageAccountKeyContext = Get-AzureStorageKey $name
    while($StorageAccountKeyContext.OperationStatus -ne "Succeeded") { }
    "DefaultEndpointsProtocol=https;AccountName=$($name);AccountKey=$($StorageAccountKeyContext.Primary)";
}

function Get-AzureSdkPath {
    param($azureSdkPath)
    if(!$azureSdkPath) {
        Join-Path $AzureToolsRoot ".NET SDK\2012-10"
    } else {
        $azureSdkPath
    }
}

function Select-ListItem {
    param($items, [scriptblock]$displayBlock, $title)
    Write-Host $title
    $counter = 1;
    $items | Foreach {
        $display = $displayBlock.Invoke($_)
        Write-Host "$counter) $display"
        $counter++
    }

    $selection = -1;
    while($selection -lt 0) {
        $selectionString = Read-Host "Enter your selection [1-$($counter-1)]"
        $temp = -1;
        if([String]::IsNullOrWhitespace($selectionString)) {
            Write-Host "Nothing specified."
        } elseif(![Int32]::TryParse($selectionString, [ref]$temp)) {
            Write-Host "Not a valid integer."
        } elseif(($temp -lt 1) -or ($temp -ge $counter)) {
            Write-Host "$temp is not a valid selection."
        } else {
            $selection = $temp
        }
    }
    $items[$selection - 1]
}

function ExtractPassword {
    param($cred)
    $Password = "";
    try {
        $unmanagedString = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($cred.Password)
        $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($unmanagedString)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($unmanagedString);
    }
    $Password
}

function YesNoPrompt {
    param($text)

    $result = $false;
    $loop = $true
    do {
        $answer = Read-Host $text
        if("Yes".StartsWith($answer, "OrdinalIgnoreCase") -or [String]::IsNullOrEmpty($answer)) {
            $loop = $false
            $result = $true;
        } elseif("No".StartsWith($answer, "OrdinalIgnoreCase")) {
            $loop = $false;
        }else {
            Write-Host "Unexpected answer..."
        }
    } while($loop)
    $result
}

function SelectOrUseProvided {
    param($Provided, $AllItems, [scriptblock]$Condition, $ObjectName, [scriptblock]$GetName)
    $Output = $null;
    $items = $AllItems;
    if(!($items -is [System.Array])) {
        $items = [Linq.Enumerable]::ToArray($AllItems);
    }
    if(!$Provided) {
        Write-Host "Loading Available $($ObjectName)s..."
        $AvailableServices = $items | Where-Object $Condition
        $Output = Select-ListItem $AvailableServices $GetName "Select $ObjectName"
    } else {
        $Output = @($items | Where-Object { ($GetName.Invoke($_)) -like "*$Provided*"})[0]
        if(!$Output) {
            throw "No $ObjectName matching $Provided"
        }
    }
    $Output
}


function require-Module {
    param([string]$name)
    if(-not(Get-Module -name $name)){
        if(Get-Module -ListAvailable | Where-Object { $_.name -eq $name }) {
           write-host "Module is avalible and will be loaded."
           Import-Module -name $name  
        } else {
            write-error "The module '$name' is required."
            exit 1
        }
    }
}

function programfiles-dir {
    if (is64bit -eq $true) {
        (Get-Item "Env:ProgramFiles(x86)").Value
    } else {
        (Get-Item "Env:ProgramFiles").Value
    }
}

function is64bit() {
    return ([IntPtr]::Size -eq 8)
}

$AzureToolsRoot = "$env:ProgramFiles\Microsoft SDKs\Windows Azure\"
$UseEmulator = $env:NUGET_GALLERY_USE_EMULATOR -eq $true