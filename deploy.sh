#!/bin/bash
set -e

# Configuration
APP_NAME="pi-dashboard"
PUBLISH_DIR="./publish"
INSTALL_DIR="/opt/pi-dashboard"
SERVICE_NAME="pi-dashboard"

echo "Building and publishing $APP_NAME..."
dotnet publish -c Release -o "$PUBLISH_DIR"

echo "Updating service file..."
sudo cp ./pi-dashboard.service /etc/systemd/system/
sudo systemctl daemon-reload

echo "Stopping service..."
sudo systemctl stop "$SERVICE_NAME" 2>/dev/null || true

echo "Deploying to $INSTALL_DIR..."
sudo mkdir -p "$INSTALL_DIR"
sudo cp -r "$PUBLISH_DIR"/* "$INSTALL_DIR"/

echo "Starting service..."
sudo systemctl start "$SERVICE_NAME"

echo "Done. Checking status..."
sudo systemctl status "$SERVICE_NAME" --no-pager
