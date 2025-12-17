#!/usr/bin/env python3
"""
NHTSA Automotive Data Explorer
Explores complaints, recalls, and TSB data for RFT dataset creation.
"""

import os
import pandas as pd
from pathlib import Path

# Data directory
SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR / "raw_data"


def load_complaints(sample_size: int = 10000) -> pd.DataFrame:
    """Load NHTSA complaints data."""
    # Column definitions from CMPL.txt data dictionary
    columns = [
        "CMPLID", "ODESSION", "MFR_NAME", "MAESSION", "MODESSION", "YEESSION",
        "CRASH", "FAILDATE", "FIRE", "INJURED", "DEATHS", "COMPDESC",
        "CITY", "STATE", "VIN", "DESSION", "DATEOFISSUE", "LDATE",
        "CDESCR", "CRASH_DT", "TESSION", "ATESSION", "EECESSION",
        "VTESSION", "VDSESSION", "VSPEED"
    ]
    
    # The actual file uses tab-separated values
    file_path = DATA_DIR / "FLAT_CMPL.txt"
    if not file_path.exists():
        print(f"❌ File not found: {file_path}")
        print("   Run download_nhtsa_data.sh first")
        return pd.DataFrame()
    
    print(f"📖 Loading complaints from {file_path}...")
    # Read with tab separator, sample for exploration
    df = pd.read_csv(
        file_path,
        sep="\t",
        encoding="latin-1",
        on_bad_lines="skip",
        nrows=sample_size
    )
    print(f"   Loaded {len(df):,} complaints")
    return df


def load_recalls(sample_size: int = 10000) -> pd.DataFrame:
    """Load NHTSA recalls data."""
    file_path = DATA_DIR / "FLAT_RCL.txt"
    if not file_path.exists():
        # Try alternative name
        file_path = DATA_DIR / "RECALLS.txt"
    if not file_path.exists():
        print(f"❌ Recalls file not found in {DATA_DIR}")
        return pd.DataFrame()
    
    print(f"📖 Loading recalls from {file_path}...")
    df = pd.read_csv(
        file_path,
        sep="\t",
        encoding="latin-1",
        on_bad_lines="skip",
        nrows=sample_size
    )
    print(f"   Loaded {len(df):,} recalls")
    return df


def explore_data(df: pd.DataFrame, name: str):
    """Print exploration summary for a dataframe."""
    print(f"\n{'='*60}")
    print(f"📊 {name} Summary")
    print(f"{'='*60}")
    print(f"Shape: {df.shape}")
    print(f"\nColumns ({len(df.columns)}):")
    for col in df.columns:
        non_null = df[col].notna().sum()
        print(f"  - {col}: {non_null:,} non-null values")
    
    print(f"\nFirst 3 rows:")
    print(df.head(3).to_string())


def show_sample_complaints(df: pd.DataFrame, n: int = 5):
    """Show sample complaint descriptions for RFT training."""
    print(f"\n{'='*60}")
    print("📝 Sample Complaint Descriptions (for RFT training)")
    print(f"{'='*60}")
    
    # Find the description column
    desc_cols = [c for c in df.columns if "DESC" in c.upper() or "CDESCR" in c.upper()]
    if not desc_cols:
        print("No description column found")
        return
    
    desc_col = desc_cols[0]
    make_col = [c for c in df.columns if "MFR" in c.upper() or "MAKE" in c.upper()]
    model_col = [c for c in df.columns if "MODEL" in c.upper()]
    year_col = [c for c in df.columns if "YEAR" in c.upper()]
    
    samples = df.dropna(subset=[desc_col]).head(n)
    
    for idx, row in samples.iterrows():
        print(f"\n--- Complaint #{idx} ---")
        if make_col:
            print(f"Make: {row.get(make_col[0], 'N/A')}")
        if model_col:
            print(f"Model: {row.get(model_col[0], 'N/A')}")
        if year_col:
            print(f"Year: {row.get(year_col[0], 'N/A')}")
        print(f"Description:\n{row[desc_col][:500]}...")


def main():
    print("🚗 NHTSA Automotive Data Explorer")
    print("=" * 60)
    
    if not DATA_DIR.exists():
        print(f"\n❌ Data directory not found: {DATA_DIR}")
        print("   Run: bash download_nhtsa_data.sh")
        return
    
    print(f"\n📁 Data directory: {DATA_DIR}")
    print("Files found:")
    for f in DATA_DIR.iterdir():
        size_mb = f.stat().st_size / (1024 * 1024)
        print(f"  - {f.name}: {size_mb:.1f} MB")
    
    # Load and explore complaints
    complaints = load_complaints(sample_size=1000)
    if not complaints.empty:
        explore_data(complaints, "Complaints")
        show_sample_complaints(complaints)
    
    # Load and explore recalls
    recalls = load_recalls(sample_size=1000)
    if not recalls.empty:
        explore_data(recalls, "Recalls")


if __name__ == "__main__":
    main()

