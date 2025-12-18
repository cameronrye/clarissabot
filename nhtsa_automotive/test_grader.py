#!/usr/bin/env python3
"""
Test the NHTSA grader with sample responses.
Validates that the grader scores correct/incorrect answers appropriately.
"""

import json
import sys
from pathlib import Path

# Add graders directory to path
sys.path.insert(0, str(Path(__file__).parent / "graders"))
from nhtsa_grader import grade, get_recalls, get_complaints, get_safety_rating


def test_recall_grader():
    """Test grading of recall queries."""
    print("=" * 60)
    print("🔍 Testing Recall Grader")
    print("=" * 60)
    
    item = {
        "make": "Acura", "model": "RDX", "year": "2012",
        "query_type": "recalls"
    }
    
    # Get actual data first
    api_data = get_recalls("Acura", "RDX", "2012")
    print(f"\n📡 API says: {api_data.get('Count', 0)} recalls found")
    if api_data.get("results"):
        for r in api_data["results"][:2]:
            print(f"   - {r.get('NHTSACampaignNumber')}: {r.get('Summary', '')[:60]}...")
    
    # Test correct response
    good_response = {"output_text": "Yes, there are 2 recalls for the 2012 Acura RDX. Campaign numbers: 19V182000 (Takata airbags) and 16V061000."}
    score = grade(item, good_response)
    print(f"\n✅ Good response score: {score:.2f}")
    
    # Test incorrect response
    bad_response = {"output_text": "No, there are no recalls for the 2012 Acura RDX."}
    score = grade(item, bad_response)
    print(f"❌ Bad response score: {score:.2f}")


def test_complaint_count_grader():
    """Test grading of complaint count queries."""
    print("\n" + "=" * 60)
    print("🔍 Testing Complaint Count Grader")
    print("=" * 60)
    
    item = {
        "make": "Tesla", "model": "Model 3", "year": "2020",
        "query_type": "complaint_count"
    }
    
    # Get actual data
    api_data = get_complaints("Tesla", "Model 3", "2020")
    actual_count = api_data.get("count", 0)
    print(f"\n📡 API says: {actual_count} complaints")
    
    # Test correct response
    good_response = {"output_text": f"There are {actual_count} complaints filed for the 2020 Tesla Model 3."}
    score = grade(item, good_response)
    print(f"\n✅ Exact count response score: {score:.2f}")
    
    # Test close response (within tolerance)
    close_response = {"output_text": f"There are approximately {actual_count - 10} complaints for this vehicle."}
    score = grade(item, close_response)
    print(f"🔶 Close count response score: {score:.2f}")
    
    # Test wrong response
    bad_response = {"output_text": "There are only 5 complaints for the 2020 Tesla Model 3."}
    score = grade(item, bad_response)
    print(f"❌ Wrong count response score: {score:.2f}")


def test_safety_rating_grader():
    """Test grading of safety rating queries."""
    print("\n" + "=" * 60)
    print("🔍 Testing Safety Rating Grader")
    print("=" * 60)
    
    item = {
        "make": "Toyota", "model": "Camry", "year": "2024",
        "query_type": "safety_rating",
        "rating_field": "OverallRating"
    }
    
    # Get actual data
    rating = get_safety_rating("Toyota", "Camry", "2024")
    if rating:
        actual = rating.get("OverallRating", "N/A")
        print(f"\n📡 API says: {actual}-star overall rating")
    
    # Test correct response
    good_response = {"output_text": "The 2024 Toyota Camry has a 5-star overall safety rating from NHTSA."}
    score = grade(item, good_response)
    print(f"\n✅ Correct rating response score: {score:.2f}")
    
    # Test wrong response
    bad_response = {"output_text": "The 2024 Toyota Camry has a 3-star safety rating."}
    score = grade(item, bad_response)
    print(f"❌ Wrong rating response score: {score:.2f}")


def test_safety_features_grader():
    """Test grading of safety features queries."""
    print("\n" + "=" * 60)
    print("🔍 Testing Safety Features Grader")
    print("=" * 60)
    
    item = {
        "make": "Toyota", "model": "Camry", "year": "2024",
        "query_type": "safety_features",
        "feature": "NHTSAForwardCollisionWarning"
    }
    
    rating = get_safety_rating("Toyota", "Camry", "2024")
    if rating:
        fcw = rating.get("NHTSAForwardCollisionWarning", "N/A")
        print(f"\n📡 API says: Forward Collision Warning is '{fcw}'")
    
    # Test correct response
    good_response = {"output_text": "Yes, Forward Collision Warning is standard on the 2024 Toyota Camry."}
    score = grade(item, good_response)
    print(f"\n✅ Correct feature response score: {score:.2f}")
    
    # Test wrong response  
    bad_response = {"output_text": "No, Forward Collision Warning is not included on this model."}
    score = grade(item, bad_response)
    print(f"❌ Wrong feature response score: {score:.2f}")


def main():
    print("🚗 NHTSA Grader Test Suite")
    print("Testing grader against live API data...\n")
    
    test_recall_grader()
    test_complaint_count_grader()
    test_safety_rating_grader()
    test_safety_features_grader()
    
    print("\n" + "=" * 60)
    print("✅ All grader tests complete!")
    print("=" * 60)


if __name__ == "__main__":
    main()

