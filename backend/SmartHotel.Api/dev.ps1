$port = 5094
$process = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "--> Clearing port $port (PID: $($process.OwningProcess))..." -ForegroundColor Cyan
    Stop-Process -Id $process.OwningProcess -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}
Write-Host "--> Launching SmartHotel Backend on port $port..." -ForegroundColor Green
dotnet run
