param(
    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$BaseUrl = "http://localhost:5000/",

    [int]$Count = 100,

    [int]$StartIndex = 1,

    [string[]]$Categories = @("image", "email", "debug", "music", "all"),

    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($SkipCertificateCheck) {
    add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy {
    public static bool Validator(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) { return true; }
}
"@
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($sender, $cert, $chain, $errors) return $true }
}

$normalizedBaseUrl = $BaseUrl.TrimEnd("/")
$endpoint = "$normalizedBaseUrl/api/mcp-services"

$authHeader = $Token
if (-not $authHeader.StartsWith("Bearer ")) {
    $authHeader = "Bearer $authHeader"
}

$headers = @{
    Authorization  = $authHeader
    "Content-Type" = "application/json"
}

Write-Host "Seeding $Count MCP services to $endpoint" -ForegroundColor Cyan

$created = 0
for ($i = 0; $i -lt $Count; $i++) {
    $index = $StartIndex + $i
    $category = $Categories[$i % $Categories.Count]
    $name = "demo-service-$index"
    $displayName = "Demo Service $index"

    $body = @{
        name = $name
        displayName = $displayName
        description = "Seeded MCP service $index for scroll testing"
        category = $category
        iconUrl = "https://example.com/icons/$category.png"
        endpointRoute = "/mcp/$category"
        isEnabled = $true
        isFree = $true
        displayOrder = $index
        version = "1.0.$index"
        author = "seed-script"
        documentationUrl = "https://example.com/docs/$name"
        tags = "seed,scroll-test,$category"
    } | ConvertTo-Json -Depth 4

    try {
        $response = Invoke-RestMethod -Method Post -Uri $endpoint -Headers $headers -Body $body
        if ($response -and $response.success) {
            $created++
            Write-Host "[$created/$Count] Created $name" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Failed to create ${name}: $($response | ConvertTo-Json -Depth 4)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "[ERROR] Failed to create ${name}: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Done. Created $created of $Count services." -ForegroundColor Cyan
