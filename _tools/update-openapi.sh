#!/bin/bash
# Update OpenAPI specification from running API
# This script works with Aspire's dynamic port assignment

set -e

echo "🔄 Updating OpenAPI specification..."
echo ""

# Function to find API port from Aspire
find_api_port() {
    # Try to find the API process
    local pid=$(ps aux | grep "BookStore.ApiService" | grep -v grep | awk '{print $2}' | head -1)
    
    if [ -z "$pid" ]; then
        return 1
    fi
    
    # Get the HTTPS port (usually the second LISTEN port)
    local port=$(lsof -p "$pid" 2>/dev/null | grep LISTEN | awk '{print $9}' | cut -d: -f2 | sort -u | tail -1)
    
    if [ -z "$port" ]; then
        return 1
    fi
    
    echo "$port"
    return 0
}

# Check if API is running and find its port
echo "🔍 Looking for running API..."
API_PORT=$(find_api_port)

if [ -z "$API_PORT" ]; then
    echo "❌ API is not running"
    echo ""
    echo "Please start the API first:"
    echo "  aspire run"
    echo ""
    echo "Or check the Aspire dashboard for the API URL"
    exit 1
fi

echo "✓ Found API on port $API_PORT"
echo ""

# Try HTTPS first, then HTTP
echo "📥 Downloading OpenAPI spec..."
if curl -k -s "https://localhost:$API_PORT/openapi/v1.json" -o openapi.json 2>/dev/null && [ -s openapi.json ]; then
    echo "✅ Downloaded from https://localhost:$API_PORT/openapi/v1.json"
elif curl -s "http://localhost:$API_PORT/openapi/v1.json" -o openapi.json 2>/dev/null && [ -s openapi.json ]; then
    echo "✅ Downloaded from http://localhost:$API_PORT/openapi/v1.json"
else
    echo "❌ Failed to download OpenAPI spec"
    echo ""
    echo "Try manually from Aspire dashboard:"
    echo "  1. Open Aspire dashboard (usually https://localhost:17161)"
    echo "  2. Find apiservice endpoint"
    echo "  3. Navigate to /openapi/v1.json"
    echo "  4. Save to openapi.json in repository root"
    exit 1
fi

# Verify the file
if [ ! -s openapi.json ]; then
    echo "❌ OpenAPI spec file is empty"
    exit 1
fi

echo ""
echo "✅ OpenAPI specification updated successfully!"
echo ""
echo "📝 Next steps:"
echo "  1. Review changes: git diff openapi.json"
echo "  2. Update client interface: ./_tools/generate-client-nswag.sh (optional)"
echo "  3. Or manually update: src/Client/BookStore.Client/IBookStoreApi.cs"
echo "  4. Build project: dotnet build"
echo "  5. Commit changes: git add openapi.json && git commit -m 'Update OpenAPI spec'"
echo ""
