#!/bin/bash

# FlexStorage Development Runner
# This script sets up environment variables and runs the API in development mode

echo "🚀 Starting FlexStorage API in Development Mode..."

# Set development environment variables
export ASPNETCORE_ENVIRONMENT=Development
export AWS_REGION=us-east-1
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_ENDPOINT_URL=http://localhost:4566

# Check if LocalStack is running
if ! curl -s http://localhost:4566/_localstack/health > /dev/null; then
    echo "⚠️  LocalStack is not running. Please start it first:"
    echo "   ./scripts/start-localstack.sh"
    exit 1
fi

echo "✅ LocalStack is running"
echo "🌐 API will be available at: http://localhost:5000"
echo "📊 Swagger UI: http://localhost:5000/swagger"
echo ""

# Navigate to backend directory and run the API
cd backend
dotnet run --project src/FlexStorage.API