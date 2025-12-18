#!/bin/bash
# NHTSA Data Download Script
# Downloads complaints, recalls, investigations, and TSB data for RFT training

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_DIR="${SCRIPT_DIR}/raw_data"

echo "🚗 NHTSA Automotive Data Downloader"
echo "====================================="
echo "Data will be saved to: ${DATA_DIR}"
echo ""

# Create data directory
mkdir -p "${DATA_DIR}"
cd "${DATA_DIR}"

# Base URLs for NHTSA flat files
BASE_URL="https://static.nhtsa.gov/odi/ffdd"

echo "📥 Downloading Complaints Data..."
echo "   This is the largest file (~336MB zipped)"
curl -L -o FLAT_CMPL.zip "${BASE_URL}/cmpl/FLAT_CMPL.zip" --progress-bar

echo ""
echo "📥 Downloading Recalls Data (post-2010)..."
curl -L -o FLAT_RCL_POST_2010.zip "${BASE_URL}/rcl/FLAT_RCL_POST_2010.zip" --progress-bar

echo ""
echo "📥 Downloading Investigations Data..."
curl -L -o FLAT_INV.zip "${BASE_URL}/inv/FLAT_INV.zip" --progress-bar

echo ""
echo "📥 Downloading Data Dictionaries..."
curl -L -o CMPL.txt "${BASE_URL}/cmpl/CMPL.txt"
curl -L -o RCL.txt "${BASE_URL}/rcl/RCL.txt"
curl -L -o INV.txt "${BASE_URL}/inv/INV.txt"

echo ""
echo "📥 Downloading TSBs (Technical Service Bulletins) 2020-2024..."
curl -L -o TSBS_RECEIVED_2020-2024.zip "${BASE_URL}/tsbs/TSBS_RECEIVED_2020-2024.zip" --progress-bar

echo ""
echo "📥 Downloading TSBs (Technical Service Bulletins) 2025..."
curl -L -o TSBS_RECEIVED_2025-2025.zip "${BASE_URL}/tsbs/TSBS_RECEIVED_2025-2025.zip" --progress-bar

echo ""
echo "📥 Downloading TSB Data Dictionary..."
curl -L -o TSBS.txt "${BASE_URL}/tsbs/TSBS.txt"

echo ""
echo "📦 Extracting archives..."
for zipfile in *.zip; do
    echo "   Extracting ${zipfile}..."
    unzip -o "$zipfile" -d .
done

echo ""
echo "✅ Download complete!"
echo ""
echo "📊 Files downloaded:"
ls -lh "${DATA_DIR}"
echo ""
echo "📖 Next steps:"
echo "   1. Run: python explore_nhtsa_data.py"
echo "   2. Or use the NHTSA API for real-time queries"

