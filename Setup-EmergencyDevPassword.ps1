# ==============================================================================
# Aarohi Core - Emergency Developer Credential Provisioning Script
# ==============================================================================
# Purpose: Sets up offline emergency developer login (Dev@Aarohi) on any Windows PC.
# Security: Uses PBKDF2-HMAC-SHA256 (100k iterations) + Windows DPAPI encryption.
# ==============================================================================

param(
    [string]$Password = ""
)

Add-Type -AssemblyName System.Security

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Aarohi Core - Emergency Dev Password Provisioning Utility" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$inputPwd = $Password
if ([string]::IsNullOrWhiteSpace($inputPwd)) {
    if ([Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
        $inputPwd = Read-Host "Enter developer password to set (press Enter for '1234')"
    }
    if ([string]::IsNullOrWhiteSpace($inputPwd)) {
        $inputPwd = "1234"
    }
}

# 1. Generate 16-byte cryptographically secure random salt
$salt = New-Object byte[] 16
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($salt)

# 2. Derive 32-byte key using PBKDF2-HMAC-SHA256 (100,000 iterations)
$derive = New-Object System.Security.Cryptography.Rfc2898DeriveBytes ($inputPwd, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$hash = $derive.GetBytes(32)

$saltB64 = [Convert]::ToBase64String($salt)
$hashB64 = [Convert]::ToBase64String($hash)
$verifier = "`$PBKDF2`$V1`$100000`$$saltB64`$$hashB64"

# 3. Encrypt the verifier using Windows DPAPI (CurrentUser Scope)
$verifierBytes = [System.Text.Encoding]::UTF8.GetBytes($verifier)
$encrypted = [System.Security.Cryptography.ProtectedData]::Protect(
    $verifierBytes, 
    $null, 
    [System.Security.Cryptography.DataProtectionScope]::CurrentUser
)
$cipherB64 = [Convert]::ToBase64String($encrypted)

# 4. Save to CurrentUser registry for all Aarohi applications
$apps = @("IMTS", "IPTS", "OPTS")
foreach ($app in $apps) {
    $regPath = "HKCU:\Software\Aarohi Embedded Systems Pvt Ltd\$app\Credentials"
    if (!(Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }
    Set-ItemProperty -Path $regPath -Name "EmergencyDevVerifier" -Value $cipherB64 -Type String
    Write-Host " [OK] Provisioned for $app" -ForegroundColor Green
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SUCCESS! Emergency Dev Login is now configured on this PC." -ForegroundColor Green
Write-Host " Username: Dev@Aarohi" -ForegroundColor Yellow
Write-Host " Password: $inputPwd" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

if ([Environment]::UserInteractive -and [Console]::IsInputRedirected -eq $false) {
    Write-Host "Press any key to exit..."
    [void][System.Console]::ReadKey($true)
}
