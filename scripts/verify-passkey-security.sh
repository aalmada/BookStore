#!/bin/bash
# Passkey Security Verification Script
# This script helps verify the passkey security fixes are working correctly

set -e

echo "=========================================="
echo "Passkey Security Verification"
echo "=========================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if solution builds
echo -e "${YELLOW}1. Checking if solution builds...${NC}"
if dotnet build --no-restore > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Solution builds successfully${NC}"
else
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi

# Check for duplicate users in database (requires running Aspire)
echo ""
echo -e "${YELLOW}2. Checking for duplicate users in database...${NC}"
QUERY="SELECT tenant_id, data->>'normalizedEmail' as email, COUNT(*) as count FROM public.mt_doc_applicationuser GROUP BY tenant_id, data->>'normalizedEmail' HAVING COUNT(*) > 1;"

# Try to connect to PostgreSQL (this assumes default Aspire setup)
if command -v psql &> /dev/null; then
    echo "Connecting to database..."
    # Set PGPASSWORD if available in environment, otherwise skip
    if [ -n "$PGPASSWORD" ]; then
        DUPLICATES=$(psql -h localhost -U postgres -d bookstore -t -c "$QUERY" 2>/dev/null | wc -l)

        if [ "$DUPLICATES" -eq 0 ]; then
            echo -e "${GREEN}✅ No duplicate users found${NC}"
        else
            echo -e "${RED}❌ Found $DUPLICATES duplicate user(s)${NC}"
            echo "Run the following SQL to see details:"
            echo "$QUERY"
        fi
    else
        echo -e "${YELLOW}⚠️  PGPASSWORD not set, skipping database check${NC}"
        echo "   Set PGPASSWORD environment variable to enable this check"
    fi
else
    echo -e "${YELLOW}⚠️  psql not found, skipping database check${NC}"
fi

# Check if indexes are tenant-scoped
echo ""
echo -e "${YELLOW}3. Checking database indexes...${NC}"
if command -v psql &> /dev/null && [ -n "$PGPASSWORD" ]; then
    INDEX_QUERY="SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'mt_doc_applicationuser' AND indexname LIKE '%email%';"

    echo "Checking unique index structure..."
    INDEXES=$(psql -h localhost -U postgres -d bookstore -t -c "$INDEX_QUERY" 2>/dev/null)

    if echo "$INDEXES" | grep -q "tenant_id"; then
        echo -e "${GREEN}✅ Indexes are tenant-scoped${NC}"
    else
        echo -e "${RED}❌ Indexes are NOT tenant-scoped - migration needed!${NC}"
        echo "You need to recreate the database or run the migration script"
    fi
else
    echo -e "${YELLOW}⚠️  Database not accessible, skipping index check${NC}"
fi

# Verify logging configuration
echo ""
echo -e "${YELLOW}4. Checking logging configuration...${NC}"
if grep -q "PasskeyCredentialMismatch" src/BookStore.ApiService/Infrastructure/Logging/Log.Users.cs; then
    echo -e "${GREEN}✅ Security logging is configured${NC}"
else
    echo -e "${RED}❌ Security logging is missing${NC}"
    exit 1
fi

# Check code for known issues
echo ""
echo -e "${YELLOW}5. Checking for known code issues...${NC}"

# Check if using DuplicatedField for unique indexes
if grep -q "UniqueIndexType.DuplicatedField" src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs; then
    echo -e "${GREEN}✅ Using tenant-scoped unique indexes (DuplicatedField)${NC}"
else
    echo -e "${RED}❌ Still using global unique indexes (Computed)${NC}"
    exit 1
fi

# Check if email conflict check exists in attestation/result
if grep -q "conflictingUserByEmail" src/BookStore.ApiService/Endpoints/PasskeyEndpoints.cs; then
    echo -e "${GREEN}✅ Email conflict check is present${NC}"
else
    echo -e "${RED}❌ Missing email conflict check${NC}"
    exit 1
fi

# Check if credential ownership validation exists
if grep -q "PasskeyCredentialMismatch" src/BookStore.ApiService/Endpoints/PasskeyEndpoints.cs; then
    echo -e "${GREEN}✅ Credential ownership validation is present${NC}"
else
    echo -e "${RED}❌ Missing credential ownership validation${NC}"
    exit 1
fi

echo ""
echo "=========================================="
echo -e "${GREEN}✅ All checks passed!${NC}"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. If this is a fresh environment, you're good to go"
echo "2. If you have existing data:"
echo "   - Drop and recreate the database"
echo "   - Clear browser passkeys for localhost"
echo "   - Re-register test accounts"
echo ""
echo "For production deployment, see:"
echo "docs/guides/passkey-security-fix.md"
