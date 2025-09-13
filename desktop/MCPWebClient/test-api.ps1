# Test script for MCP Web Client
# This script demonstrates how to use the web client API with persistent MCP server

Write-Host "=== MCP Web Client Test Script ===" -ForegroundColor Green
Write-Host "This script will test the web API endpoints with persistent MCP server connection" -ForegroundColor Yellow
Write-Host ""

# Function to test API endpoint
function Test-MCPEndpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Body = $null
    )
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = @{ "Content-Type" = "application/json" }
        }
        
        if ($Body) {
            $params.Body = $Body | ConvertTo-Json
        }
        
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Host "Error calling $Url : $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Check if web client is running
Write-Host "1. Testing health endpoint..." -ForegroundColor Cyan
$health = Test-MCPEndpoint -Url "https://localhost:5001/health"
if ($health) {
    Write-Host "✅ Health check: $($health.status)" -ForegroundColor Green
} else {
    Write-Host "❌ Web client is not running. Please start it with 'dotnet run' in the MCPWebClient directory." -ForegroundColor Red
    Write-Host "💡 The web client will automatically start and manage a persistent MCP server." -ForegroundColor Yellow
    exit 1
}

Write-Host "`n⏱️  Waiting for MCP server to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Check server status
Write-Host "`n2. Testing MCP status endpoint..." -ForegroundColor Cyan
$status = Test-MCPEndpoint -Url "https://localhost:5001/api/mcp/status"
if ($status) {
    Write-Host "✅ MCP Status: $($status.status)" -ForegroundColor Green
    Write-Host "Available commands:" -ForegroundColor Yellow
    foreach ($cmd in $status.availableCommands) {
        Write-Host "  - $cmd" -ForegroundColor White
    }
}

# Test command execution
Write-Host "`n3. Testing command execution with persistent connection..." -ForegroundColor Cyan
$command = @{ command = "launch ob studio" }
$result = Test-MCPEndpoint -Url "https://localhost:5001/api/mcp/execute" -Method "POST" -Body $command

if ($result) {
    if ($result.success) {
        Write-Host "✅ Command executed successfully via persistent MCP connection!" -ForegroundColor Green
        Write-Host "Message: $($result.message)" -ForegroundColor White
        if ($result.details) {
            Write-Host "Details: $($result.details)" -ForegroundColor Gray
        }
    } else {
        Write-Host "❌ Command failed: $($result.message)" -ForegroundColor Red
    }
}

# Test multiple commands to verify persistent connection
Write-Host "`n4. Testing multiple commands to verify persistent connection..." -ForegroundColor Cyan
$commands = @(
    "start ob",
    "launch ob studio",
    "open ob"
)

foreach ($cmd in $commands) {
    Write-Host "Testing: '$cmd'" -ForegroundColor White
    $testCommand = @{ command = $cmd }
    $testResult = Test-MCPEndpoint -Url "https://localhost:5001/api/mcp/execute" -Method "POST" -Body $testCommand
    
    if ($testResult -and $testResult.success) {
        Write-Host "  ✅ Success" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Failed" -ForegroundColor Red
    }
    Start-Sleep -Seconds 1
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green
Write-Host "🌐 Open your browser to https://localhost:5001 to use the web interface!" -ForegroundColor Yellow
Write-Host "📚 Visit https://localhost:5001/swagger for API documentation" -ForegroundColor Cyan
Write-Host "🔧 The web client maintains a persistent connection to the MCP server for optimal performance!" -ForegroundColor Green
