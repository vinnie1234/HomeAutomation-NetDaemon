# PowerShell Script to build a .NET project, clean a directory, and move build output

# WARNING, MUCH IN THIS FILE WILL NEED TO BE CONFIGURED FOR YOUR PATHS, SETTINGS, ETC...
# READ COMMENTS CAREFULLY

# Local paths on your computer
$solutionPath = "D:\Projects\HomeAutomation-NetDaemon\automation"
$solutionFilename = "Automation.csproj"

# ND folder served up by a samba addon in HA
$buildPath = "\\192.168.50.189\config\netdaemon6"

# Step 1: Run dotnet build
Set-Location $solutionPath

# Don't know why, but ND *really* wants this folder to exist even though we're only working with binaries
New-Item -Path $buildPath\apps -ItemType Directory -Force

# Step 2: Delete everything in the network share except 'logs' and 'apps' folders
Write-Host "Cleaning network share..."
Get-ChildItem -Path $buildPath -Exclude "logs","apps", ".storage" | ForEach-Object {
    if ($_.PSIsContainer -and $_.Name -ne "logs" -and $_.Name -ne "apps" -and $_.Name -ne ".storage") {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    } elseif (!$_.PSIsContainer) {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $_.FullName) {
        Write-Warning "Failed to delete item: $($_.FullName)"
    }
}

# Build the local project and output to where it should go in ND share
& dotnet build $solutionPath\$solutionFilename --output $buildPath

Write-Host "Built and deployed, now restarting ND addon"

# You will unfortunately need to turn off protected mode on the "terminal & ssh” addon for this line to work:
# Replace options in below line with your correct user, host, private key path, port, and addon ID
ssh vinnie1234@192.168.50.189 -i "C:\Users\Vince\.ssh\id_ed25519" -m hmac-sha2-512-etm@openssh.com 'sudo docker restart addon_c6a2317c_netdaemon6'

Write-Host "ND addon restarted - Script execution completed."
Write-Host ""
Write-Host "Open logs folder? Press Y to open, any other key to not: " -NoNewLine

$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

if ($key.Character -eq 'y')
{	
	explorer "\\192.168.50.189\config\netdaemon6\logs"
}

Set-Location "D:\Projects\HomeAutomation-NetDaemon\automation\Scripts"
