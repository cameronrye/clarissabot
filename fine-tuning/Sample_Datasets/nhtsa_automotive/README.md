# NHTSA Automotive Dataset for RFT

This folder contains tools to download and explore NHTSA (National Highway Traffic Safety Administration) data for building Reinforcement Fine-Tuning (RFT) datasets.

## 🚗 What's Available

| Dataset | Description | Size | Update Frequency |
|---------|-------------|------|-----------------|
| **Complaints** | Consumer complaints about vehicle defects | ~336MB | Daily |
| **Recalls** | Safety recall campaigns | ~43MB | Daily |
| **Investigations** | NHTSA defect investigations | ~43MB | Daily |
| **TSBs** | Technical Service Bulletins from manufacturers | ~30MB each | Daily |
| **Safety Ratings** | NCAP crash test ratings | API only | Annual |

## 🔧 Quick Start

### Option 1: Use the API (Recommended for RFT)

The NHTSA API is free, requires no authentication, and provides real-time data:

```bash
# Test the API
bash test_api.sh

# Example API calls:
# Get recalls for a vehicle
curl "https://api.nhtsa.gov/recalls/recallsByVehicle?make=acura&model=rdx&modelYear=2012"

# Get complaints for a vehicle
curl "https://api.nhtsa.gov/complaints/complaintsByVehicle?make=Tesla&model=Model%203&modelYear=2020"

# Get safety ratings
curl "https://api.nhtsa.gov/SafetyRatings/modelyear/2024/make/Toyota/model/Camry"
```

### Option 2: Download Bulk Data

```bash
# Download all datasets (~500MB total)
bash download_nhtsa_data.sh

# Explore the data
python3 explore_nhtsa_data.py
```

## 🎯 RFT Use Cases

### 1. Recall Lookup (Exact Match Grading)
```
User: "Is my 2012 Acura RDX under any recalls?"
Expected: Call API → "Yes, 2 recalls found: 19V182000 (Takata airbags), 16V061000 (Airbags)"
Grader: String match against API response
```

### 2. Safety Ratings (Numeric Grading)
```
User: "What's the safety rating for a 2024 Toyota Camry?"
Expected: "5-star overall rating (front: 5, side: 5, rollover: 5)"
Grader: Numeric match against API
```

### 3. Complaint Analysis (Model Grading)
```
User: "What are common problems with 2020 Tesla Model 3?"
Expected: Summarize top complaint categories (airbags, suspension, FSD)
Grader: Model grader checks if summary matches complaint patterns
```

## 📊 API Endpoints

| Endpoint | Description |
|----------|-------------|
| `/recalls/recallsByVehicle` | Recalls by Year/Make/Model |
| `/recalls/campaignNumber` | Recall by campaign number |
| `/complaints/complaintsByVehicle` | Complaints by Year/Make/Model |
| `/SafetyRatings/modelyear/{year}/make/{make}/model/{model}` | NCAP ratings |
| `/products/vehicle/makes` | Available makes for a year |
| `/products/vehicle/models` | Available models for make/year |

## 📁 Files

- `download_nhtsa_data.sh` - Downloads bulk data files
- `explore_nhtsa_data.py` - Python script to explore downloaded data
- `test_api.sh` - Quick API test script
- `raw_data/` - Downloaded data files (after running download script)

## 🔗 Resources

- [NHTSA Datasets and APIs](https://www.nhtsa.gov/nhtsa-datasets-and-apis)
- [NHTSA Recall Search](https://www.nhtsa.gov/recalls)
- [NHTSA Complaints](https://www.nhtsa.gov/report-a-safety-problem)

