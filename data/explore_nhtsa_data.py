#!/usr/bin/env python3
"""
NHTSA Automotive Data Explorer
Explores complaints, recalls, and TSB data for RFT dataset creation.
No external dependencies - uses only Python standard library.
"""

import csv
import json
from pathlib import Path
from typing import Optional
from urllib.request import urlopen
from urllib.error import URLError

SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR / "raw_data"
API_BASE = "https://api.nhtsa.gov"


def load_tsv_file(file_path: Path, max_rows: int = 1000) -> tuple[list[str], list[list[str]]]:
    """Load a TSV file and return headers and rows."""
    if not file_path.exists():
        return [], []

    rows = []
    headers = []
    with open(file_path, "r", encoding="latin-1", errors="replace") as f:
        reader = csv.reader(f, delimiter="\t")
        for i, row in enumerate(reader):
            if i == 0:
                headers = row
            elif i <= max_rows:
                rows.append(row)
            else:
                break
    return headers, rows


def explore_file(name: str, file_path: Path, max_rows: int = 1000):
    """Explore a downloaded data file."""
    headers, rows = load_tsv_file(file_path, max_rows)
    if not headers:
        print(f"❌ Could not load {file_path}")
        return

    print(f"\n{'='*60}")
    print(f"📊 {name} Summary")
    print(f"{'='*60}")
    print(f"Rows loaded: {len(rows):,} (sample)")
    print(f"Columns ({len(headers)}):")
    for col in headers[:15]:  # Show first 15 columns
        print(f"  - {col}")
    if len(headers) > 15:
        print(f"  ... and {len(headers) - 15} more columns")

    # Show sample rows
    if rows:
        print(f"\nSample row:")
        for h, v in zip(headers[:8], rows[0][:8]):
            print(f"  {h}: {v[:60]}..." if len(v) > 60 else f"  {h}: {v}")


def api_query(endpoint: str) -> Optional[dict]:
    """Query the NHTSA API."""
    url = f"{API_BASE}{endpoint}"
    try:
        with urlopen(url, timeout=10) as resp:
            return json.loads(resp.read().decode())
    except (URLError, json.JSONDecodeError) as e:
        print(f"❌ API error: {e}")
        return None


def explore_api():
    """Explore data via the NHTSA API."""
    print(f"\n{'='*60}")
    print("🌐 Live API Exploration")
    print(f"{'='*60}")

    # Sample recalls query
    print("\n📋 Sample Recalls (2020 Tesla Model 3):")
    data = api_query("/recalls/recallsByVehicle?make=Tesla&model=Model%203&modelYear=2020")
    if data and data.get("Count", 0) > 0:
        for r in data["results"][:3]:
            print(f"  - {r.get('NHTSACampaignNumber', 'N/A')}: {r.get('Summary', 'N/A')[:80]}...")

    # Sample complaints query
    print("\n� Sample Complaints (2022 Ford Mustang):")
    data = api_query("/complaints/complaintsByVehicle?make=Ford&model=Mustang&modelYear=2022")
    if data and data.get("count", 0) > 0:
        for c in data["results"][:3]:
            summary = c.get("summary", "N/A")[:100]
            print(f"  - {c.get('components', 'N/A')}: {summary}...")

    # Safety ratings
    print("\n⭐ Safety Ratings (2024 Honda Accord):")
    data = api_query("/SafetyRatings/modelyear/2024/make/Honda/model/Accord")
    if data and data.get("Count", 0) > 0:
        for v in data["Results"][:2]:
            vid = v.get("VehicleId")
            desc = v.get("VehicleDescription", "N/A")
            # Get full rating
            rating_data = api_query(f"/SafetyRatings/VehicleId/{vid}")
            if rating_data and rating_data.get("Results"):
                r = rating_data["Results"][0]
                print(f"  {desc}:")
                print(f"    Overall: {r.get('OverallRating', 'N/A')}⭐")
                print(f"    Front Crash: {r.get('OverallFrontCrashRating', 'N/A')}⭐")
                print(f"    Side Crash: {r.get('OverallSideCrashRating', 'N/A')}⭐")


def main():
    print("🚗 NHTSA Automotive Data Explorer")
    print("=" * 60)
    print("No external dependencies - uses Python standard library only")

    # Check for downloaded data
    if DATA_DIR.exists():
        print(f"\n📁 Data directory: {DATA_DIR}")
        print("Files found:")
        for f in sorted(DATA_DIR.iterdir()):
            if f.is_file():
                size_mb = f.stat().st_size / (1024 * 1024)
                print(f"  - {f.name}: {size_mb:.1f} MB")

        # Explore downloaded files
        cmpl_file = DATA_DIR / "FLAT_CMPL.txt"
        if cmpl_file.exists():
            explore_file("Complaints", cmpl_file)

        rcl_file = DATA_DIR / "FLAT_RCL.txt"
        if rcl_file.exists():
            explore_file("Recalls", rcl_file)
    else:
        print(f"\n⚠️  No downloaded data found at {DATA_DIR}")
        print("   Run: bash download_nhtsa_data.sh")
        print("   Or use the API exploration below...")

    # Always show API exploration
    explore_api()

    print(f"\n{'='*60}")
    print("✅ Exploration complete!")
    print("📖 API Docs: https://www.nhtsa.gov/nhtsa-datasets-and-apis")


if __name__ == "__main__":
    main()

