#!/bin/bash
# Generate API client using Refitter (Refit interfaces + DTOs)
# Generates clean Refit interfaces from OpenAPI spec

set -e

echo "🔄 Generating API client with Refitter..."
echo ""

# Check if openapi.json exists
if [ ! -f "openapi.json" ]; then
    echo "❌ openapi.json not found"
    echo ""
    echo "Run ./_tools/update-openapi.sh first to download the spec"
    exit 1
fi

# Generate Refit client with DTOs
echo "📝 Generating Refit interfaces + DTOs..."
refitter --settings-file .refitter --skip-validation

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Client generated successfully!"
    echo ""
    echo "📝 Generated files:"
    echo "  - 28 Refit interface files (I*Endpoint.cs)"
    echo "  - 1 Contracts file (Contracts.cs with all DTOs)"
    echo ""
    echo "📝 Next steps:"
    echo "  1. Review generated files: src/Client/BookStore.Client/"
    echo "  2. Build project: dotnet build"
    echo "  3. Commit changes: git add openapi.json src/Client/BookStore.Client/"
    echo "  4. Commit message: git commit -m 'Update API client from OpenAPI spec'"
    echo ""
else
    echo ""
    echo "❌ Client generation failed"
    exit 1
fi
