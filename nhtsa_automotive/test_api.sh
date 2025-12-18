#!/bin/bash
# NHTSA API Quick Test Script
# Tests various API endpoints with sample queries

BASE_URL="https://api.nhtsa.gov"

echo "🚗 NHTSA API Quick Test"
echo "========================"
echo ""

echo "📋 Test 1: Recalls for 2012 Acura RDX"
echo "--------------------------------------"
curl -s "${BASE_URL}/recalls/recallsByVehicle?make=acura&model=rdx&modelYear=2012" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(f'Found {d[\"Count\"]} recalls')"
echo ""

echo "📋 Test 2: Complaints for 2020 Tesla Model 3"
echo "---------------------------------------------"
curl -s "${BASE_URL}/complaints/complaintsByVehicle?make=Tesla&model=Model%203&modelYear=2020" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(f'Found {d[\"count\"]} complaints')"
echo ""

echo "⭐ Test 3: Safety Ratings for 2024 Toyota Camry"
echo "-----------------------------------------------"
curl -s "${BASE_URL}/SafetyRatings/modelyear/2024/make/Toyota/model/Camry" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(f'Found {d[\"Count\"]} variants: ' + ', '.join([v['VehicleDescription'] for v in d['Results']]))"
echo ""

echo "🏭 Test 4: Available Makes for 2024"
echo "-----------------------------------"
curl -s "${BASE_URL}/products/vehicle/makes?modelYear=2024&issueType=r" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); makes=[m['make'] for m in d['results'][:10]]; print(f'Found {len(d[\"results\"])} makes. Sample: ' + ', '.join(makes))"
echo ""

echo "✅ All API tests completed!"
echo ""
echo "📖 API Documentation: https://www.nhtsa.gov/nhtsa-datasets-and-apis"

