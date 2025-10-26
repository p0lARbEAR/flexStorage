#!/bin/bash

echo "🛑 Stopping FlexStorage LocalStack Environment..."

# Stop LocalStack
docker-compose down

echo "✅ LocalStack stopped!"