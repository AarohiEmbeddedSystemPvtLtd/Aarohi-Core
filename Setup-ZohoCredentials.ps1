param(
    [string]$SmtpAppPassword = "",
    [string]$SenderEmail = "panel@aarohies.in",
    [string]$ClientId = "",
    [string]$ClientSecret = "",
    [string]$RefreshToken = "",
    [string]$SmtpHost = "smtp.zoho.in",
    [int]$SmtpPort = 587
)

# ==============================================================================
# Aarohi Core - Zoho Credentials DPAPI Provisioning Utility
# ==============================================================================
# Purpose: Securely provisions Zoho SMTP and OAuth credentials into Windows DPAPI.
# Security: Passwords and tokens are encrypted with Windows DPAPI (CurrentUser Scope).
#           Zero credentials are stored in plaintext.
# ==============================================================================

Add-Type -AssemblyName System.Security

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Aarohi Core - Zoho Credentials Provisioning Utility    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

function Protect-Data([string]$plain) {
    if ([string]::IsNullOrWhiteSpace($plain)) { return "" }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($plain.Trim())
    $encrypted = [System.Security.Cryptography.ProtectedData]::Protect(
        $bytes, 
        $null, 
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser
    )
    return [Convert]::ToBase64String($encrypted)
}

# 1. Interactive input if not provided as command parameters
$inputSmtpPassword = $SmtpAppPassword
if ([string]::IsNullOrWhiteSpace($inputSmtpPassword)) {
    if ([Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
        $inputSmtpPassword = Read-Host "Enter Zoho SMTP Application Password"
    }
}

$inputSenderEmail = $SenderEmail
if ([string]::IsNullOrWhiteSpace($inputSenderEmail)) {
    $inputSenderEmail = "panel@aarohies.in"
}

$inputClientId = $ClientId
$inputClientSecret = $ClientSecret
$inputRefreshToken = $RefreshToken

if ([string]::IsNullOrWhiteSpace($inputClientId) -and [Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
    Write-Host ""
    $setupOAuth = Read-Host "Configure Zoho OAuth REST API (Client ID, Secret, Refresh Token)? (y/n) [default: n]"
    if ($setupOAuth -eq "y" -or $setupOAuth -eq "Y") {
        $inputClientId = Read-Host "Enter Zoho OAuth Client ID"
        $inputClientSecret = Read-Host "Enter Zoho OAuth Client Secret"
        $inputRefreshToken = Read-Host "Enter Zoho OAuth Refresh Token"
    }
}

# 2. Target application registry paths
$apps = @("IMTS", "IPTS", "OPTS")

Write-Host ""
Write-Host "Configuring Windows DPAPI credentials..." -ForegroundColor Yellow

foreach ($app in $apps) {
    $credPath = "HKCU:\Software\Aarohi Embedded Systems Pvt Ltd\$app\Credentials"
    $settPath = "HKCU:\Software\Aarohi Embedded Systems Pvt Ltd\$app\Settings"

    if (!(Test-Path $credPath)) { New-Item -Path $credPath -Force | Out-Null }
    if (!(Test-Path $settPath)) { New-Item -Path $settPath -Force | Out-Null }

    # Save SMTP Configuration
    Set-ItemProperty -Path $credPath -Name "SmtpSenderEmail" -Value $inputSenderEmail -Type String
    Set-ItemProperty -Path $settPath -Name "SmtpHost" -Value $SmtpHost -Type String
    Set-ItemProperty -Path $settPath -Name "SmtpPort" -Value $SmtpPort -Type DWord

    if (![string]::IsNullOrWhiteSpace($inputSmtpPassword)) {
        $encryptedSmtp = Protect-Data $inputSmtpPassword
        Set-ItemProperty -Path $credPath -Name "SmtpAppPassword" -Value $encryptedSmtp -Type String
        Write-Host " [OK] Encrypted SMTP App Password saved for $app" -ForegroundColor Green
    }

    # Save OAuth API Configuration (if provided)
    if (![string]::IsNullOrWhiteSpace($inputClientId)) {
        Set-ItemProperty -Path $credPath -Name "ZohoClientId" -Value $inputClientId.Trim() -Type String
        Set-ItemProperty -Path $credPath -Name "ZohoSenderEmail" -Value $inputSenderEmail -Type String
    }
    if (![string]::IsNullOrWhiteSpace($inputClientSecret)) {
        $encryptedSecret = Protect-Data $inputClientSecret
        Set-ItemProperty -Path $credPath -Name "ZohoClientSecret" -Value $encryptedSecret -Type String
    }
    if (![string]::IsNullOrWhiteSpace($inputRefreshToken)) {
        $encryptedToken = Protect-Data $inputRefreshToken
        Set-ItemProperty -Path $credPath -Name "ZohoRefreshToken" -Value $encryptedToken -Type String
        Write-Host " [OK] Encrypted Zoho OAuth Credentials saved for $app" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SUCCESS! Zoho credentials securely stored via DPAPI." -ForegroundColor Green
Write-Host " Sender Email : $inputSenderEmail" -ForegroundColor Yellow
Write-Host " SMTP Server  : ${SmtpHost}:${SmtpPort}" -ForegroundColor Yellow
Write-Host " DPAPI Scope  : CurrentUser (Local Windows User Profile)" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

if ([Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
    Write-Host "Press any key to exit..."
    [void][System.Console]::ReadKey($true)
}
