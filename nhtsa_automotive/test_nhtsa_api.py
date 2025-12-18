#!/usr/bin/env python3
"""
NHTSA API Tester
Tests the NHTSA API endpoints for real-time automotive data queries.
Perfect for building an RFT dataset with verifiable ground truth.
"""

import json
import requests
from typing import Optional

BASE_URL = "https://api.nhtsa.gov"


def get_recalls(make: str, model: str, year: int) -> dict:
    """Get recalls for a specific vehicle."""
    url = f"{BASE_URL}/recalls/recallsByVehicle"
    params = {"make": make, "model": model, "modelYear": year}
    response = requests.get(url, params=params)
    return response.json()


def get_complaints(make: str, model: str, year: int) -> dict:
    """Get complaints for a specific vehicle."""
    url = f"{BASE_URL}/complaints/complaintsByVehicle"
    params = {"make": make, "model": model, "modelYear": year}
    response = requests.get(url, params=params)
    return response.json()


def get_safety_ratings(year: int, make: str, model: str) -> dict:
    """Get NCAP safety ratings for a vehicle."""
    url = f"{BASE_URL}/SafetyRatings/modelyear/{year}/make/{make}/model/{model}"
    response = requests.get(url)
    return response.json()


def get_recall_by_campaign(campaign_number: str) -> dict:
    """Get recall details by NHTSA campaign number."""
    url = f"{BASE_URL}/recalls/campaignNumber"
    params = {"campaignNumber": campaign_number}
    response = requests.get(url, params=params)
    return response.json()


def get_available_makes(year: int) -> dict:
    """Get all vehicle makes for a given year."""
    url = f"{BASE_URL}/products/vehicle/makes"
    params = {"modelYear": year, "issueType": "r"}  # r = recalls
    response = requests.get(url, params=params)
    return response.json()


def get_available_models(year: int, make: str) -> dict:
    """Get all models for a given year and make."""
    url = f"{BASE_URL}/products/vehicle/models"
    params = {"modelYear": year, "make": make, "issueType": "r"}
    response = requests.get(url, params=params)
    return response.json()


def demo_api():
    """Demonstrate API capabilities with sample queries."""
    print("🚗 NHTSA API Demo")
    print("=" * 60)
    
    # Test 1: Get recalls for 2012 Acura RDX
    print("\n📋 Test 1: Recalls for 2012 Acura RDX")
    print("-" * 40)
    recalls = get_recalls("Acura", "RDX", 2012)
    print(f"Found {recalls.get('Count', 0)} recalls")
    if recalls.get('results'):
        for r in recalls['results'][:2]:  # Show first 2
            print(f"\n  Campaign: {r.get('NHTSACampaignNumber', 'N/A')}")
            print(f"  Component: {r.get('Component', 'N/A')}")
            print(f"  Summary: {r.get('Summary', 'N/A')[:200]}...")
    
    # Test 2: Get complaints for 2020 Tesla Model 3
    print("\n\n📋 Test 2: Complaints for 2020 Tesla Model 3")
    print("-" * 40)
    complaints = get_complaints("Tesla", "Model 3", 2020)
    print(f"Found {complaints.get('Count', 0)} complaints")
    if complaints.get('results'):
        for c in complaints['results'][:2]:  # Show first 2
            print(f"\n  Component: {c.get('Component', 'N/A')}")
            print(f"  Summary: {c.get('Summary', 'N/A')[:200]}...")
    
    # Test 3: Get safety ratings for 2024 Toyota Camry
    print("\n\n⭐ Test 3: Safety Ratings for 2024 Toyota Camry")
    print("-" * 40)
    ratings = get_safety_ratings(2024, "Toyota", "Camry")
    print(f"Found {ratings.get('Count', 0)} vehicle variants")
    if ratings.get('Results'):
        for v in ratings['Results'][:2]:
            print(f"\n  Variant: {v.get('VehicleDescription', 'N/A')}")
            print(f"  Vehicle ID: {v.get('VehicleId', 'N/A')}")
    
    # Test 4: Available makes for 2024
    print("\n\n🏭 Test 4: Available Makes for 2024")
    print("-" * 40)
    makes = get_available_makes(2024)
    print(f"Found {makes.get('Count', 0)} makes")
    if makes.get('results'):
        sample_makes = [m.get('make') for m in makes['results'][:10]]
        print(f"Sample: {', '.join(sample_makes)}")
    
    print("\n\n✅ API Demo Complete!")
    print("\nThese endpoints can be used for RFT grading:")
    print("  - Recall lookups: exact match grading")
    print("  - Safety ratings: numeric match grading")
    print("  - Complaint analysis: model grader for similarity")


if __name__ == "__main__":
    demo_api()

