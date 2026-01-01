#!/bin/bash
# Generate API client using NSwag (supports OpenAPI 3.1)
# Generates Refit interface in BookStore.Client project

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
echo "📝 Generating Refit interface..."
nswag openapi2csclient \
    /input:openapi.json \
    /output:src/Client/BookStore.Client/IBookStoreApi.cs \
    /namespace:BookStore.Client \
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
    echo "  1. Review generated interface: src/Client/BookStore.Client/IBookStoreApi.cs"
    echo "  2. Build project: dotnet build"
    echo "  3. Commit changes: git add openapi.json src/Client/BookStore.Client/IBookStoreApi.cs"
    echo "  4. Commit message: git commit -m 'Update API client from OpenAPI spec'"
    echo ""
else
    echo ""
    echo "❌ Client generation failed"
    exit 1
fi
