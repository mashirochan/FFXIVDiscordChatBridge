param (
    [string]$filePath
)

# Read the content of the .csproj file
[xml]$xml = Get-Content $filePath

# Select the Version element
$versionElement = $xml.Project.PropertyGroup.Version

# Split the version into parts
$versionParts = $versionElement -split '\.'

# Increment the last part
$versionParts[-1] = [int]$versionParts[-1] + 1

# Join the parts back together
$newVersion = $versionParts -join '.'

# Set the new version in the XML
$xml.Project.PropertyGroup.Version = $newVersion

# Save the updated XML back to the file
$xml.Save($filePath)
