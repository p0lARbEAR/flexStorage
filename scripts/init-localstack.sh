#!/bin/bash

# Wait for LocalStack to be ready
echo "Waiting for LocalStack to be ready..."
until curl -s http://localhost:4566/_localstack/health | grep -q '"s3": "available"'; do
  echo "Waiting for S3 service..."
  sleep 2
done

echo "LocalStack is ready! Initializing S3 buckets..."

# Set AWS CLI to use LocalStack
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1
export AWS_ENDPOINT_URL=http://localhost:4566

# Create S3 buckets for FlexStorage
echo "Creating S3 buckets..."

# Deep Archive bucket
aws s3 mb s3://flexstorage-deep-archive --endpoint-url=http://localhost:4566
echo "✅ Created flexstorage-deep-archive bucket"

# Flexible Retrieval bucket  
aws s3 mb s3://flexstorage-flexible --endpoint-url=http://localhost:4566
echo "✅ Created flexstorage-flexible bucket"

# List buckets to verify
echo "📋 Available buckets:"
aws s3 ls --endpoint-url=http://localhost:4566

echo "🎉 LocalStack initialization complete!"