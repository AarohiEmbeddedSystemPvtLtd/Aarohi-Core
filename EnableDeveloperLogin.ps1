# ==============================================================================
# Aarohi Core - Developer Login Provisioning Utility
# ==============================================================================
# Detects installed Aarohi applications from Windows uninstall registrations and
# provisions the offline Dev@Aarohi verifier only for those applications.
# ==============================================================================

Add-Type -AssemblyName System.Security

$companyRegistryRoot = "HKCU:\Software\Aarohi Embedded Systems Pvt Ltd"
$expectedPublisher = "aarohiembeddedsystemspvtltd"
$supportedApplications = @(
    @{ Name = "IMTS"; Pattern = "(^|[^A-Z0-9])IMTS([^A-Z0-9]|$)" },
    @{ Name = "IPTS"; Pattern = "(^|[^A-Z0-9])IPTS([^A-Z0-9]|$)" },
    @{ Name = "OPTS"; Pattern = "(^|[^A-Z0-9])OPTS([^A-Z0-9]|$)" }
)

function Wait-ForExit {
    if ([Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
        Write-Host ""
        Write-Host "Press any key to exit..."
        [void][System.Console]::ReadKey($true)
    }
}

function Stop-WithError([string]$Message) {
    Write-Host ""
    Write-Host "ERROR: $Message" -ForegroundColor Red
    Wait-ForExit
    exit 1
}

function Get-NormalizedPublisher([string]$Publisher) {
    if ([string]::IsNullOrWhiteSpace($Publisher)) {
        return ""
    }

    return ($Publisher -replace "[^A-Za-z0-9]", "").ToLowerInvariant()
}

function Get-InstalledAarohiApplications {
    $uninstallLocations = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    $detectedApplications = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($location in $uninstallLocations) {
        $installedPrograms = Get-ItemProperty -Path $location -ErrorAction SilentlyContinue

        foreach ($program in $installedPrograms) {
            if ((Get-NormalizedPublisher $program.Publisher) -ne $expectedPublisher) {
                continue
            }

            $displayName = [string]$program.DisplayName
            if ([string]::IsNullOrWhiteSpace($displayName)) {
                continue
            }

            foreach ($application in $supportedApplications) {
                if ($displayName -match $application.Pattern) {
                    [void]$detectedApplications.Add($application.Name)
                    break
                }
            }
        }
    }

    return @($detectedApplications | Sort-Object)
}

function Read-RequiredPassword {
    if (![Environment]::UserInteractive -or [Console]::IsInputRedirected) {
        Stop-WithError "Interactive password entry is required."
    }

    while ($true) {
        $securePassword = Read-Host "Enter developer password" -AsSecureString
        $passwordPointer = [IntPtr]::Zero

        try {
            $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
            $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        }
        finally {
            if ($passwordPointer -ne [IntPtr]::Zero) {
                [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
            }
            if ($null -ne $securePassword) {
                $securePassword.Dispose()
            }
        }

        if (![string]::IsNullOrWhiteSpace($plainPassword)) {
            return $plainPassword
        }

        Write-Host "Password cannot be empty. Please try again." -ForegroundColor Yellow
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       Aarohi Developer Login Provisioning Utility       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$installedApplications = Get-InstalledAarohiApplications
if ($installedApplications.Count -eq 0) {
    Stop-WithError "No supported Aarohi application published by Aarohi Embedded Systems Pvt Ltd was found. No changes were made."
}

Write-Host "Detected installed Aarohi applications:" -ForegroundColor Yellow
foreach ($applicationName in $installedApplications) {
    Write-Host " - $applicationName" -ForegroundColor Green
}

$inputPassword = Read-RequiredPassword

try {
    $salt = New-Object byte[] 16
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $random.GetBytes($salt)

    $derive = New-Object System.Security.Cryptography.Rfc2898DeriveBytes (
        $inputPassword,
        $salt,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256
    )
    $hash = $derive.GetBytes(32)

    $saltBase64 = [Convert]::ToBase64String($salt)
    $hashBase64 = [Convert]::ToBase64String($hash)
    $verifier = "`$PBKDF2`$V1`$100000`$$saltBase64`$$hashBase64"

    $verifierBytes = [System.Text.Encoding]::UTF8.GetBytes($verifier)
    $encryptedVerifier = [System.Security.Cryptography.ProtectedData]::Protect(
        $verifierBytes,
        $null,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser
    )
    $cipherBase64 = [Convert]::ToBase64String($encryptedVerifier)

    foreach ($applicationName in $installedApplications) {
        $credentialsPath = Join-Path $companyRegistryRoot "$applicationName\Credentials"

        if (!(Test-Path $credentialsPath)) {
            New-Item -Path $credentialsPath -Force -ErrorAction Stop | Out-Null
        }

        Set-ItemProperty `
            -Path $credentialsPath `
            -Name "EmergencyDevVerifier" `
            -Value $cipherBase64 `
            -Type String `
            -ErrorAction Stop

        Write-Host " [OK] Developer login enabled for $applicationName" -ForegroundColor Green
    }
}
catch {
    Stop-WithError "Developer login could not be provisioned. $($_.Exception.Message)"
}
finally {
    if ($null -ne $derive) {
        $derive.Dispose()
    }
    if ($null -ne $random) {
        $random.Dispose()
    }
    $inputPassword = $null
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SUCCESS! Developer login was enabled for the applications" -ForegroundColor Green
Write-Host " listed above." -ForegroundColor Green
Write-Host " Username: Dev@Aarohi" -ForegroundColor Yellow
Write-Host " DPAPI Scope: Current Windows User" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

Wait-ForExit
exit 0
