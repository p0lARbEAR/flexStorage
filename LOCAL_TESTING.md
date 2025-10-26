# 🧪 FlexStorage Local Testing Guide

This guide will help you set up and test FlexStorage locally using LocalStack to simulate AWS services without needing real AWS credentials or incurring costs.

## 📋 Prerequisites

Before you begin, make sure you have the following installed:

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [AWS CLI](https://aws.amazon.com/cli/) (for testing LocalStack)
- [Postman](https://www.postman.com/downloads/) or similar API testing tool

## ⚙️ Environment Setup

### 1. Configure Environment Variables (Optional)

You can use environment variables to override configuration settings:

```bash
# Copy the example environment file
cp .env.example .env

# Edit the .env file with your preferred settings
# The default values work for LocalStack development
```

**Available Environment Variables:**
- `AWS_REGION` - AWS region (default: us-east-1)
- `AWS_ACCESS_KEY_ID` - AWS access key (default: test for LocalStack)
- `AWS_SECRET_ACCESS_KEY` - AWS secret key (default: test for LocalStack)
- `AWS_ENDPOINT_URL` - S3 endpoint URL (default: http://localhost:4566 for LocalStack)
- `AWS_S3_DEEP_ARCHIVE_BUCKET` - Deep archive bucket name
- `AWS_S3_FLEXIBLE_BUCKET` - Flexible retrieval bucket name

## 🚀 Quick Start

### 1. Start LocalStack Environment

```bash
# Make scripts executable (first time only)
chmod +x scripts/*.sh

# Start LocalStack and initialize S3 buckets
./scripts/start-localstack.sh
```

This will:
- Start LocalStack container with S3 and Glacier services
- Create the required S3 buckets (`flexstorage-deep-archive` and `flexstorage-flexible`)
- Display helpful information about endpoints and testing commands

### 2. Start FlexStorage API

You have several options to start the API:

#### Option 1: Use the Development Script (Recommended)
```bash
./scripts/run-dev.sh
```

#### Option 2: Set Environment Variables Manually
```bash
export ASPNETCORE_ENVIRONMENT=Development
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_ENDPOINT_URL=http://localhost:4566
cd backend && dotnet run --project src/FlexStorage.API
```

#### Option 3: Use .env File
```bash
# Copy and customize environment file
cp .env.example .env
# Edit .env with your preferred values (optional - defaults work for LocalStack)

# Run the API
cd backend
dotnet run --project src/FlexStorage.API
```

#### Option 4: Direct Run (Uses appsettings.Development.json)
```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/FlexStorage.API
```

The API will start on `http://localhost:5000` with:
- ✅ In-memory database for development
- ✅ LocalStack S3 integration
- ✅ API key authentication
- ✅ Swagger UI at `http://localhost:5000/swagger`

## 🔑 API Testing with Postman

### Step 1: Generate an API Key

**Request:**
```http
POST http://localhost:5000/api/auth/apikey
Content-Type: application/json

{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "description": "LocalStack Test Key",
  "expiresInDays": 30
}
```

**Expected Response:**
```json
{
  "apiKey": "fsk_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890",
  "expiresAt": "2025-11-25T15:30:00.000Z",
  "message": "API key generated successfully. Store it securely - you won't be able to see it again."
}
```

**💡 Tip:** Save the `apiKey` value - you'll need it for all subsequent requests!

### Step 2: Validate Your API Key

**Request:**
```http
GET http://localhost:5000/api/auth/validate
X-API-Key: fsk_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890
```

**Expected Response:**
```json
{
  "isValid": true,
  "userId": "123e4567-e89b-12d3-a456-426614174000"
}
```

### Step 3: Test S3 Connection (Debugging)

Before uploading files, test the S3 connection:

**Request:**
```http
GET http://localhost:5000/api/test/s3-connection
```

**Expected Response:**
```json
{
  "success": true,
  "message": "S3 connection successful",
  "buckets": [
    {
      "name": "flexstorage-deep-archive",
      "creationDate": "2025-10-26T15:53:38.000Z"
    },
    {
      "name": "flexstorage-flexible", 
      "creationDate": "2025-10-26T15:53:44.000Z"
    }
  ],
  "endpoint": "http://localhost:4566"
}
```

**Test S3 Upload:**
```http
POST http://localhost:5000/api/test/s3-upload-test
```

### Step 4: Upload a File

**Request:**
```http
POST http://localhost:5000/api/files/upload
X-API-Key: fsk_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890
Content-Type: multipart/form-data

[Select a file to upload in Postman's Body > form-data]
```

**Expected Response:**
```json
{
  "success": true,
  "fileId": "456e7890-e12b-34d5-a678-901234567890",
  "fileName": "your-file.jpg",
  "size": 1024000,
  "storageLocation": "s3-glacier-deep://flexstorage-deep-archive/2025/10/26/your-file.jpg"
}
```

### Step 4: List Your Files

**Request:**
```http
GET http://localhost:5000/api/files
X-API-Key: fsk_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890
```

### Step 5: Download a File

**Request:**
```http
GET http://localhost:5000/api/files/{fileId}/download
X-API-Key: fsk_AbCdEfGhIjKlMnOpQrStUvWxYz1234567890
```

## 🔍 Verifying LocalStack Storage

You can verify that files are actually stored in LocalStack using AWS CLI:

```bash
# Set environment variables for LocalStack
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1
export AWS_ENDPOINT_URL=http://localhost:4566

# List all buckets
aws s3 ls --endpoint-url=http://localhost:4566

# List files in deep archive bucket
aws s3 ls s3://flexstorage-deep-archive/ --recursive --endpoint-url=http://localhost:4566

# List files in flexible bucket
aws s3 ls s3://flexstorage-flexible/ --recursive --endpoint-url=http://localhost:4566

# Download a file from LocalStack
aws s3 cp s3://flexstorage-deep-archive/2025/10/26/your-file.jpg ./downloaded-file.jpg --endpoint-url=http://localhost:4566
```

## 🧪 Running Tests

### Run All Tests

```bash
cd backend
dotnet test --verbosity normal
```

### Run Specific Test Projects

```bash
# Domain tests
dotnet test tests/FlexStorage.Domain.Tests/ --verbosity normal

# Application tests (includes API key tests)
dotnet test tests/FlexStorage.Application.Tests/ --verbosity normal

# Infrastructure tests
dotnet test tests/FlexStorage.Infrastructure.Tests/ --verbosity normal
```

## 🐛 Troubleshooting

### LocalStack Issues

**Problem:** LocalStack container won't start
```bash
# Check if port 4566 is already in use
lsof -i :4566

# Stop any existing LocalStack containers
docker stop flexstorage-localstack
docker rm flexstorage-localstack

# Restart LocalStack
./scripts/start-localstack.sh
```

**Problem:** S3 buckets not created
```bash
# Manually initialize buckets
./scripts/init-localstack.sh
```

### API Issues

**Problem:** "The AWS Access Key Id you provided does not exist in our records"
- This usually means the S3 client is connecting to real AWS instead of LocalStack
- Verify LocalStack is running: `docker ps | grep localstack`
- Test LocalStack S3 directly: `curl "http://localhost:4566/" -H "Authorization: AWS test:test"`
- Check the test endpoint: `GET http://localhost:5000/api/test/s3-connection`
- Ensure environment variables are set: `AWS_ACCESS_KEY_ID=test`, `AWS_SECRET_ACCESS_KEY=test`
- Restart the API after making configuration changes

**Problem:** "Unable to get IAM security credentials" or "nodename nor servname provided"
- Make sure LocalStack is running: `docker ps | grep localstack`
- Verify LocalStack health: `curl http://localhost:4566/_localstack/health`
- Ensure S3 buckets exist: `curl -X PUT http://localhost:4566/flexstorage-deep-archive && curl -X PUT http://localhost:4566/flexstorage-flexible`
- Check that `ForcePathStyle = true` is set in the S3 client configuration (this is crucial for LocalStack)

**Problem:** API key authentication fails
- Ensure you're using the correct API key from the generation response
- Check that the `X-API-Key` header is set correctly
- Verify the API key hasn't expired

**Problem:** File upload fails
- Check LocalStack logs: `docker logs flexstorage-localstack`
- Verify S3 buckets exist: `aws s3 ls --endpoint-url=http://localhost:4566`
- Ensure the API is running in Development environment

## 🛑 Cleanup

### Stop LocalStack

```bash
./scripts/stop-localstack.sh
```

### Reset LocalStack Data

```bash
# Stop LocalStack
./scripts/stop-localstack.sh

# Remove LocalStack data
rm -rf /tmp/localstack

# Restart LocalStack
./scripts/start-localstack.sh
```

## 📊 LocalStack Web Interface

LocalStack provides a web interface for easier debugging:

- **Health Check:** http://localhost:4566/_localstack/health
- **S3 Browser:** Use AWS CLI or tools like [LocalStack Desktop](https://app.localstack.cloud/)

## 🎯 What's Tested Locally

With this setup, you're testing:

- ✅ **API Key Authentication** - Generate, validate, and revoke API keys
- ✅ **File Upload** - Upload files to simulated S3 Glacier storage
- ✅ **File Storage** - Files stored in LocalStack S3 buckets
- ✅ **Storage Provider Selection** - Deep Archive vs Flexible Retrieval
- ✅ **Database Operations** - In-memory database for development
- ✅ **Error Handling** - Invalid API keys, missing files, etc.
- ✅ **API Endpoints** - All REST endpoints working correctly

## 🚀 Next Steps

Once local testing is working:

1. **Integration Tests** - Run the full test suite
2. **Performance Testing** - Test with larger files
3. **Error Scenarios** - Test network failures, invalid data
4. **Production Setup** - Configure real AWS credentials for production

## � Conf iguration Options

### Environment Variables Priority

The application uses the following priority order for configuration:

1. **Environment Variables** (highest priority)
2. **appsettings.{Environment}.json** files
3. **appsettings.json** (lowest priority)

### Key Configuration Settings

| Setting | Development Default | Production Default | Description |
|---------|-------------------|-------------------|-------------|
| `AWS:Region` | us-east-1 | us-west-2 | AWS region |
| `AWS:AccessKey` | test | (not set) | AWS access key |
| `AWS:SecretKey` | test | (not set) | AWS secret key |
| `AWS:S3:ServiceURL` | http://localhost:4566 | (not set) | S3 endpoint URL |
| `AWS:S3:ForcePathStyle` | true | false | Use path-style URLs |
| `AWS:S3:UseHttp` | true | false | Use HTTP instead of HTTPS |

### Switching Between Environments

**Development (LocalStack):**
```bash
export ASPNETCORE_ENVIRONMENT=Development
./scripts/run-dev.sh
```

**Production (Real AWS):**
```bash
export ASPNETCORE_ENVIRONMENT=Production
# Set your real AWS credentials
export AWS_ACCESS_KEY_ID=your-real-access-key
export AWS_SECRET_ACCESS_KEY=your-real-secret-key
cd backend && dotnet run --project src/FlexStorage.API
```

## 💡 Tips for Effective Testing

1. **Use Postman Collections** - Save your requests for easy reuse
2. **Environment Variables** - Set up Postman environments for different configs
3. **Automated Scripts** - Use the provided shell scripts for consistent setup
4. **Monitor Logs** - Watch both API and LocalStack logs for debugging
5. **Test Edge Cases** - Try invalid API keys, large files, network interruptions
6. **Configuration Testing** - Try different environment variable combinations
7. **Use .env Files** - Keep your local settings in .env files (not committed to git)

Happy testing! 🎉