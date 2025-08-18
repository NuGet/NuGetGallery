Function Merge-Objects {
    <#
        .SYNOPSIS
            Merges a PSObject with another PSObject.

        .DESCRIPTION
            Iterates through every NoteProperty in the source object.
            Properties of the source object are added to the target object.
            If these properties already exist in the target object, they are overwritten.
            Properties that are PSObjects are merged recursively.

        .PARAMETER Source
            The object to merge the target with.

        .PARAMETER Output
            The object that the properties of the source are copied into.
            This object is mutated by the function.

        .OUTPUTS
            None

        .EXAMPLE
            $source = New-Object PSObject -Property @{ A = "A", B = New-Object PSObject -Property @{ C = "C" }, D = "D"}
            $target = New-Object PSObject -Property @{ D = "E", E = "E" }

            Merge-Objects -Source $source -Target $target

            $target is now @{ A = "A", B = New-Object PSObject -Property @{ C = "C" }, D = "D", E = "E" }
    #>
    param(
        [PSObject]$Source,
        [PSObject]$Target
    )

    # For each property of the source object, add the property to the target object
    $Source | `
        Get-Member -MemberType NoteProperty | `
        ForEach-Object {
            $name = $_.Name
            $value = $Source."$name"
            $existingValue = $Target."$name"
            if ($_.Definition.StartsWith("System.Management.Automation.PSCustomObject") -and $existingValue -is [PSObject]) {
                # If the property is a nested object in both the source and target, merge the nested object in the source with the target object
                Merge-Objects -Source $value -Target $existingValue
            } else {
                # Add the property to the target object
                # If the property already exists on the target object, overwrite it
                $Target | Add-Member -MemberType NoteProperty -Name $name -Value $value -Force
            }
        }
}

Export-ModuleMember -Function Merge-Objects