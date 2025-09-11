# Start MCP Web Client
Write-Host "🚀 Starting MCP Web Client with Persistent MCP Server..." -ForegroundColor Green
Write-Host ""

# Change to the web client directory
Set-Location "c:\Codebase\MCP\MCPWebClient"

# Check if the project builds successfully
Write-Host "🔧 Building project..." -ForegroundColor Cyan
$buildResult = & dotnet build --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}
Write-Host "✅ Build successful!" -ForegroundColor Green

Write-Host ""
Write-Host "🌐 Starting web server..." -ForegroundColor Cyan
Write-Host "📝 The web client will automatically start and manage a persistent MCP server" -ForegroundColor Yellow
Write-Host "🔗 Web interface will be available at: https://localhost:5001" -ForegroundColor Green
Write-Host "📚 API documentation will be available at: https://localhost:5001/swagger" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host ""

# Start the web client
& dotnet run
