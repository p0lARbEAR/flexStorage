#!/bin/bash

echo "🚀 Starting FlexStorage LocalStack Environment..."

# Start LocalStack
echo "📦 Starting LocalStack container..."
docker-compose up -d localstack

# Wait for LocalStack to be ready
echo "⏳ Waiting for LocalStack to be ready..."
sleep 10

# Initialize buckets
echo "🪣 Initializing S3 buckets..."
./scripts/init-localstack.sh

echo ""
echo "✅ LocalStack is ready!"
echo "🌐 LocalStack Dashboard: http://localhost:4566"
echo "📊 Health Check: http://localhost:4566/_localstack/health"
echo ""
echo "🔧 AWS CLI commands for testing:"
echo "   aws s3 ls --endpoint-url=http://localhost:4566"
echo "   aws s3 cp test.txt s3://flexstorage-deep-archive/ --endpoint-url=http://localhost:4566"
echo ""
echo "🚀 You can now start your FlexStorage API with:"
echo "   cd backend && ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/FlexStorage.API"