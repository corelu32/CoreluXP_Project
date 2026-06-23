#!/bin/bash
set -e

TOOL_DIR="LUmaKE/.tools"
mkdir -p "$TOOL_DIR"

echo "Downloading DXC for Linux..."
wget -O "$TOOL_DIR/compiler.tar.gz" -q --show-progress https://github.com/microsoft/DirectXShaderCompiler/releases/download/v1.9.2602.24/linux_dxc_2026_05_26.x86_64.tar.gz

# echo "Extracting toolchain assets directly to destination..."
# # Extracts bin/ and lib/ directly inside LUmaKE/.tools/
tar -xf "$TOOL_DIR/compiler.tar.gz" -C "$TOOL_DIR/"

# echo "Setting permissions..."
chmod +x "$TOOL_DIR/bin/dxc"

# echo "Cleaning up unneeded headers and archive..."
rm -rf "$TOOL_DIR/include" "$TOOL_DIR/share" "$TOOL_DIR/compiler.tar.gz"