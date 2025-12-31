#!/bin/bash
# Generate API client using NSwag (supports OpenAPI 3.1)
# Alternative to Refitter with better OpenAPI 3.1 support

set -e

echo "🔄 Generating API client with NSwag..."
echo ""

# Check if openapi.json exists
if [ ! -f "openapi.json" ]; then
    echo "❌ openapi.json not found"
    echo ""
    echo "Run ./_tools/update-openapi.sh first to download the spec"
    exit 1
fi

# Generate client
echo "📝 Generating C# client..."
nswag openapi2csclient \
    /input:openapi.json \
    /output:src/Web/BookStore.Web/Services/IBookStoreApi.cs \
    /namespace:BookStore.Web.Services \
    /className:BookStoreApiClient \
    /generateClientInterfaces:true \
    /generateDtoTypes:false \
    /useBaseUrl:false \
    /generateExceptionClasses:false

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Client generated successfully!"
    echo ""
    echo "📝 Next steps:"
    echo "  1. Review generated client: src/Web/BookStore.Web/Services/IBookStoreApi.cs"
    echo "  2. Build project: dotnet build"
    echo "  3. Commit changes: git add src/Web/BookStore.Web/Services/IBookStoreApi.cs"
    echo ""
else
    echo ""
    echo "❌ Client generation failed"
    exit 1
fi
