#!/usr/bin/env python3
"""
Comprehensive test harness for NHTSA grader.
Tests all query types with correct answers, synthetic wrong answers,
and edge cases to validate grader robustness.
"""

import json
import sys
from pathlib import Path
from typing import NamedTuple

# Add graders directory to path
sys.path.insert(0, str(Path(__file__).parent / "graders"))
from nhtsa_grader import grade, get_recalls, get_complaints, get_safety_rating


class TestResult(NamedTuple):
    """Result of a single test case."""
    name: str
    passed: bool
    expected: str
    actual: float
    message: str


class GraderTestSuite:
    """Comprehensive test suite for the NHTSA grader."""

    def __init__(self):
        self.results: list[TestResult] = []
        self.verbose = True

    def assert_score_range(self, name: str, score: float, min_score: float, max_score: float, context: str = ""):
        """Assert that a score falls within expected range."""
        passed = min_score <= score <= max_score
        expected = f"[{min_score:.1f}, {max_score:.1f}]"
        result = TestResult(name, passed, expected, score, context)
        self.results.append(result)
        return passed

    def assert_high_score(self, name: str, score: float, context: str = ""):
        """Assert score is high (good response)."""
        return self.assert_score_range(name, score, 0.7, 1.0, context)

    def assert_low_score(self, name: str, score: float, context: str = ""):
        """Assert score is low (bad response)."""
        return self.assert_score_range(name, score, 0.0, 0.3, context)

    def assert_neutral_score(self, name: str, score: float, context: str = ""):
        """Assert score is neutral (unclear/partial response)."""
        return self.assert_score_range(name, score, 0.3, 0.7, context)

    def test_recall_grader(self):
        """Test grading of recall queries with various response types."""
        print("\n" + "=" * 60)
        print("🔍 Testing Recall Grader")
        print("=" * 60)

        # Vehicle with known recalls
        item = {"make": "Acura", "model": "RDX", "year": "2012", "query_type": "recalls"}
        api_data = get_recalls("Acura", "RDX", "2012")
        recall_count = api_data.get("Count", 0)
        print(f"\n📡 API: {recall_count} recalls for 2012 Acura RDX")

        if recall_count > 0:
            campaigns = [r.get("NHTSACampaignNumber", "") for r in api_data.get("results", [])]

            # Correct responses
            responses = [
                (f"Yes, there are {recall_count} recalls for the 2012 Acura RDX.", "Correct count"),
                (f"Yes, there are recalls. Campaign: {campaigns[0]}", "Mentions campaign"),
                ("Yes, the 2012 Acura RDX has active recalls.", "Affirms recalls exist"),
            ]
            for text, desc in responses:
                score = grade(item, {"output_text": text})
                self.assert_high_score(f"Recall correct: {desc}", score, text[:50])

            # Wrong responses (synthetic)
            wrong_responses = [
                ("No, there are no recalls for this vehicle.", "False negative"),
                ("The 2012 Acura RDX has 0 recalls.", "Says zero recalls"),
                ("No recall information found.", "Denies recalls"),
            ]
            for text, desc in wrong_responses:
                score = grade(item, {"output_text": text})
                self.assert_low_score(f"Recall wrong: {desc}", score, text[:50])

        # Vehicle with no recalls (use recent model year)
        item_no_recall = {"make": "Toyota", "model": "Camry", "year": "2025", "query_type": "recalls"}
        api_data = get_recalls("Toyota", "Camry", "2025")
        if api_data.get("Count", 0) == 0:
            score = grade(item_no_recall, {"output_text": "No recalls found for the 2025 Toyota Camry."})
            self.assert_high_score("No recalls - correct response", score)


    def test_complaint_count_grader(self):
        """Test grading of complaint count queries."""
        print("\n" + "=" * 60)
        print("🔍 Testing Complaint Count Grader")
        print("=" * 60)

        item = {"make": "Tesla", "model": "Model 3", "year": "2020", "query_type": "complaint_count"}
        api_data = get_complaints("Tesla", "Model 3", "2020")
        actual_count = api_data.get("count", 0)
        print(f"\n📡 API: {actual_count} complaints for 2020 Tesla Model 3")

        # Exact count
        score = grade(item, {"output_text": f"There are {actual_count} complaints."})
        self.assert_high_score("Complaint count: exact", score)

        # Close count (within tolerance)
        score = grade(item, {"output_text": f"Approximately {actual_count - 10} complaints."})
        self.assert_score_range("Complaint count: close (-10)", score, 0.8, 1.0)

        # Wrong count
        score = grade(item, {"output_text": "There are only 5 complaints."})
        self.assert_low_score("Complaint count: wrong (5)", score)

    def test_safety_rating_grader(self):
        """Test grading of safety rating queries."""
        print("\n" + "=" * 60)
        print("🔍 Testing Safety Rating Grader")
        print("=" * 60)

        item = {"make": "Toyota", "model": "Camry", "year": "2024",
                "query_type": "safety_rating", "rating_field": "OverallRating"}
        rating = get_safety_rating("Toyota", "Camry", "2024")

        if rating:
            actual = rating.get("OverallRating", "N/A")
            print(f"\n📡 API: {actual}-star overall rating")

            # Correct rating
            score = grade(item, {"output_text": f"The 2024 Toyota Camry has a {actual}-star rating."})
            self.assert_high_score("Safety rating: correct", score)

            # Different format
            score = grade(item, {"output_text": f"{actual} star safety rating from NHTSA."})
            self.assert_high_score("Safety rating: alt format", score)

            # Wrong rating
            wrong_rating = "3" if actual != "3" else "2"
            score = grade(item, {"output_text": f"It has a {wrong_rating}-star rating."})
            self.assert_low_score("Safety rating: wrong", score)

    def test_safety_features_grader(self):
        """Test grading of safety features queries."""
        print("\n" + "=" * 60)
        print("🔍 Testing Safety Features Grader")
        print("=" * 60)

        item = {"make": "Toyota", "model": "Camry", "year": "2024",
                "query_type": "safety_features", "feature": "NHTSAForwardCollisionWarning"}
        rating = get_safety_rating("Toyota", "Camry", "2024")

        if rating:
            fcw = rating.get("NHTSAForwardCollisionWarning", "N/A")
            print(f"\n📡 API: Forward Collision Warning is '{fcw}'")

            if fcw.lower() == "standard":
                # Correct: says yes when standard
                score = grade(item, {"output_text": "Yes, FCW is standard."})
                self.assert_high_score("Feature: correctly says standard", score)

                # Wrong: says no when standard
                score = grade(item, {"output_text": "No, it's not included."})
                self.assert_low_score("Feature: wrongly says not included", score)

    def test_comparison_grader(self):
        """Test grading of comparison queries."""
        print("\n" + "=" * 60)
        print("🔍 Testing Comparison Grader")
        print("=" * 60)

        item = {
            "query_type": "comparison",
            "vehicles": [
                {"make": "Toyota", "model": "Camry", "year": "2024"},
                {"make": "Honda", "model": "Accord", "year": "2024"}
            ],
            "rating_field": "OverallRating"
        }

        r1 = get_safety_rating("Toyota", "Camry", "2024")
        r2 = get_safety_rating("Honda", "Accord", "2024")

        if r1 and r2:
            rating1 = r1.get("OverallRating", "N/A")
            rating2 = r2.get("OverallRating", "N/A")
            print(f"\n📡 API: Camry={rating1}★, Accord={rating2}★")

            # Mentions both ratings
            score = grade(item, {"output_text": f"Camry: {rating1} stars, Accord: {rating2} stars."})
            self.assert_high_score("Comparison: both ratings", score)

            # Mentions only one
            score = grade(item, {"output_text": f"The Camry has {rating1} stars."})
            self.assert_score_range("Comparison: one rating", score, 0.4, 0.6)

    def test_complaints_general(self):
        """Test grading of general complaint queries."""
        print("\n" + "=" * 60)
        print("🔍 Testing General Complaints Grader")
        print("=" * 60)

        item = {"make": "Honda", "model": "Accord", "year": "2020", "query_type": "complaints"}
        api_data = get_complaints("Honda", "Accord", "2020")
        count = api_data.get("count", 0)
        print(f"\n📡 API: {count} complaints for 2020 Honda Accord")

        if count > 0:
            # Acknowledges complaints exist
            score = grade(item, {"output_text": "Yes, there are reported complaints."})
            self.assert_score_range("Complaints: acknowledges issues", score, 0.7, 1.0)

            # Wrongly says no complaints
            score = grade(item, {"output_text": "No complaints have been filed."})
            self.assert_low_score("Complaints: false negative", score)

    def test_edge_cases(self):
        """Test edge cases and error handling."""
        print("\n" + "=" * 60)
        print("🔍 Testing Edge Cases")
        print("=" * 60)

        # Unknown query type - should return neutral
        item = {"query_type": "unknown_type", "make": "Toyota", "model": "Camry", "year": "2024"}
        score = grade(item, {"output_text": "Some response."})
        self.assert_neutral_score("Edge case: unknown query type", score)

        # Empty response
        item = {"make": "Toyota", "model": "Camry", "year": "2024", "query_type": "recalls"}
        score = grade(item, {"output_text": ""})
        self.assert_low_score("Edge case: empty response", score)

        # Missing output_text
        score = grade(item, {})
        self.assert_low_score("Edge case: missing output_text", score)

    def run_all_tests(self):
        """Run all tests and print summary."""
        print("🚗 NHTSA Grader Comprehensive Test Suite")
        print("Testing grader against live API data...")
        print("=" * 60)

        self.test_recall_grader()
        self.test_complaint_count_grader()
        self.test_safety_rating_grader()
        self.test_safety_features_grader()
        self.test_comparison_grader()
        self.test_complaints_general()
        self.test_edge_cases()

        # Print summary
        print("\n" + "=" * 60)
        print("📊 TEST RESULTS SUMMARY")
        print("=" * 60)

        passed = sum(1 for r in self.results if r.passed)
        failed = len(self.results) - passed

        for r in self.results:
            status = "✅" if r.passed else "❌"
            print(f"{status} {r.name}: {r.actual:.2f} (expected {r.expected})")

        print("\n" + "-" * 60)
        print(f"Total: {len(self.results)} tests | ✅ Passed: {passed} | ❌ Failed: {failed}")

        if failed == 0:
            print("\n🎉 All tests passed!")
        else:
            print(f"\n⚠️  {failed} test(s) failed - review grader logic")

        return failed == 0


def main():
    suite = GraderTestSuite()
    success = suite.run_all_tests()
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()

