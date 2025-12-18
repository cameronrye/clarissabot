"""
NHTSA API Grader for RFT
Validates model responses against live NHTSA API data.
"""

import json
import re
from urllib.request import urlopen
from urllib.error import URLError

API_BASE = "https://api.nhtsa.gov"


def api_query(endpoint: str) -> dict | None:
    """Query the NHTSA API."""
    url = f"{API_BASE}{endpoint}"
    try:
        with urlopen(url, timeout=10) as resp:
            return json.loads(resp.read().decode())
    except (URLError, json.JSONDecodeError):
        return None


def get_recalls(make: str, model: str, year: str) -> dict:
    """Get recalls for a vehicle."""
    model_encoded = model.replace(" ", "%20")
    data = api_query(f"/recalls/recallsByVehicle?make={make}&model={model_encoded}&modelYear={year}")
    return data if data else {"Count": 0, "results": []}


def get_complaints(make: str, model: str, year: str) -> dict:
    """Get complaints for a vehicle."""
    model_encoded = model.replace(" ", "%20")
    data = api_query(f"/complaints/complaintsByVehicle?make={make}&model={model_encoded}&modelYear={year}")
    return data if data else {"count": 0, "results": []}


def get_safety_rating(make: str, model: str, year: str) -> dict | None:
    """Get safety ratings for a vehicle."""
    data = api_query(f"/SafetyRatings/modelyear/{year}/make/{make}/model/{model}")
    if data and data.get("Count", 0) > 0:
        vid = data["Results"][0].get("VehicleId")
        rating_data = api_query(f"/SafetyRatings/VehicleId/{vid}")
        if rating_data and rating_data.get("Results"):
            return rating_data["Results"][0]
    return None


def grade(item: dict, sample: dict) -> float:
    """
    Grade a model response against NHTSA API data.
    
    Args:
        item: The training item with metadata (make, model, year, query_type, etc.)
        sample: The model's response (output_text)
    
    Returns:
        float: Score between 0.0 and 1.0
    """
    query_type = item.get("query_type", "")
    response = sample.get("output_text", "").lower()
    make = item.get("make", "")
    model = item.get("model", "")
    year = item.get("year", "")
    
    if query_type == "recalls":
        api_data = get_recalls(make, model, year)
        recall_count = api_data.get("Count", 0)
        
        # Check if response correctly indicates presence/absence of recalls
        has_recalls = recall_count > 0
        response_says_yes = any(w in response for w in ["yes", "recall", "found", str(recall_count)])
        response_says_no = any(w in response for w in ["no recall", "none", "0 recall"])
        
        if has_recalls and response_says_yes and not response_says_no:
            # Bonus for mentioning specific campaign numbers
            campaigns = [r.get("NHTSACampaignNumber", "") for r in api_data.get("results", [])]
            mentioned = sum(1 for c in campaigns if c.lower() in response)
            return min(1.0, 0.7 + 0.3 * (mentioned / max(len(campaigns), 1)))
        elif not has_recalls and response_says_no:
            return 1.0
        return 0.0
    
    elif query_type == "recall_count":
        api_data = get_recalls(make, model, year)
        expected = api_data.get("Count", 0)
        # Check if the count is mentioned
        if str(expected) in response:
            return 1.0
        # Partial credit for close counts
        numbers = re.findall(r'\d+', response)
        if numbers:
            closest = min(int(n) for n in numbers)
            if abs(closest - expected) <= 1:
                return 0.8
        return 0.0

    elif query_type == "complaints":
        # General complaint queries - check if response mentions complaints exist
        api_data = get_complaints(make, model, year)
        count = api_data.get("count", 0)

        has_complaints = count > 0

        # Check for negative phrases FIRST (order matters for "no complaints filed")
        response_says_no = any(phrase in response for phrase in [
            "no complaints", "no issues", "no problems", "none reported",
            "no reported", "haven't been filed", "have not been filed",
            "0 complaints", "zero complaints", "none filed", "no filed"
        ])

        # Only count as "yes" if not negated
        response_says_yes = not response_says_no and any(w in response for w in [
            "complaint", "issue", "problem", "reported", "filed", "owners report"
        ])

        # Check for component filter match if specified
        component_filter = item.get("component_filter", "").lower()
        if component_filter and component_filter in response:
            return 1.0  # Mentioned the specific component

        if has_complaints and response_says_yes:
            return 0.8  # Acknowledged complaints exist
        elif not has_complaints and response_says_no:
            return 1.0
        elif has_complaints and response_says_no:
            return 0.0  # Wrong - denied complaints when they exist
        elif not has_complaints and response_says_yes:
            return 0.0  # Wrong - claimed complaints when none exist
        return 0.5  # Neutral/unclear

    elif query_type == "complaint_count":
        api_data = get_complaints(make, model, year)
        expected = api_data.get("count", 0)
        if str(expected) in response:
            return 1.0
        # Allow some tolerance for changing data
        numbers = re.findall(r'\d+', response)
        if numbers:
            for n in numbers:
                if abs(int(n) - expected) <= 20:  # Within 20 of current count
                    return 0.9
        return 0.0
    
    elif query_type == "safety_rating":
        rating = get_safety_rating(make, model, year)
        if not rating:
            return 0.5  # Can't verify, neutral score
        
        field = item.get("rating_field", "OverallRating")
        expected = rating.get(field, "Not Rated")
        
        if str(expected) in response or f"{expected}-star" in response or f"{expected} star" in response:
            return 1.0
        return 0.0
    
    elif query_type == "safety_features":
        rating = get_safety_rating(make, model, year)
        if not rating:
            return 0.5
        
        feature = item.get("feature", "")
        feature_value = rating.get(feature, "Not Rated")
        
        is_standard = feature_value.lower() == "standard"

        # Check for negative phrases first (order matters)
        response_says_no = any(phrase in response for phrase in [
            "not standard", "not included", "not equipped", "not available",
            "optional", "is not", "does not have", "doesn't have"
        ])
        # Only count as "yes" if not negated
        response_says_yes = not response_says_no and any(
            w in response for w in ["yes", "standard", "included", "equipped", "available"]
        )

        if is_standard and response_says_yes and not response_says_no:
            return 1.0
        elif not is_standard and response_says_no:
            return 1.0
        elif is_standard and response_says_no:
            return 0.0  # Wrong - said no when it's standard
        elif not is_standard and response_says_yes:
            return 0.0  # Wrong - said yes when it's not standard
        return 0.3  # Unclear response
    
    elif query_type == "comparison":
        # For comparisons, check if both vehicles' data is correctly referenced
        vehicles = item.get("vehicles", [])
        field = item.get("rating_field", "OverallRating")
        score = 0.0
        for v in vehicles:
            rating = get_safety_rating(v["make"], v["model"], v["year"])
            if rating:
                expected = rating.get(field, "")
                if str(expected) in response:
                    score += 0.5
        return score
    
    # Default: return neutral score
    return 0.5

