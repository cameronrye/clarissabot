#!/usr/bin/env python3
"""
Generate expanded RFT training examples from NHTSA bulk data and API.
Creates diverse examples covering edge cases for better model training.
"""

import csv
import json
import random
from pathlib import Path
from urllib.request import urlopen
from urllib.error import URLError

SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR / "raw_data"
OUTPUT_FILE = SCRIPT_DIR / "rft_training_expanded.jsonl"
VALIDATION_FILE = SCRIPT_DIR / "rft_validation_expanded.jsonl"
API_BASE = "https://api.nhtsa.gov"

SYSTEM_PROMPT = "You are an automotive safety assistant with access to the NHTSA database. Answer questions about vehicle recalls, complaints, and safety ratings accurately and concisely."

# Diverse vehicle list for queries
VEHICLES = [
    # Popular sedans
    ("Toyota", "Camry", ["2020", "2021", "2022", "2023", "2024"]),
    ("Honda", "Accord", ["2020", "2021", "2022", "2023", "2024"]),
    ("Honda", "Civic", ["2019", "2020", "2021", "2022", "2023"]),
    ("Nissan", "Altima", ["2019", "2020", "2021", "2022"]),
    ("Hyundai", "Sonata", ["2020", "2021", "2022", "2023"]),
    # SUVs
    ("Toyota", "RAV4", ["2019", "2020", "2021", "2022", "2023"]),
    ("Honda", "CR-V", ["2019", "2020", "2021", "2022", "2023"]),
    ("Ford", "Explorer", ["2020", "2021", "2022", "2023"]),
    ("Chevrolet", "Equinox", ["2019", "2020", "2021", "2022"]),
    ("Jeep", "Grand Cherokee", ["2020", "2021", "2022", "2023"]),
    # Trucks
    ("Ford", "F-150", ["2019", "2020", "2021", "2022", "2023"]),
    ("Chevrolet", "Silverado", ["2019", "2020", "2021", "2022"]),
    ("Ram", "1500", ["2019", "2020", "2021", "2022"]),
    ("Toyota", "Tacoma", ["2020", "2021", "2022", "2023"]),
    # EVs
    ("Tesla", "Model 3", ["2019", "2020", "2021", "2022", "2023"]),
    ("Tesla", "Model Y", ["2020", "2021", "2022", "2023"]),
    ("Ford", "Mustang Mach-E", ["2021", "2022", "2023"]),
    ("Chevrolet", "Bolt", ["2020", "2021", "2022"]),
    # Luxury
    ("BMW", "3 Series", ["2020", "2021", "2022", "2023"]),
    ("Mercedes-Benz", "C-Class", ["2019", "2020", "2021", "2022"]),
    ("Lexus", "RX", ["2020", "2021", "2022", "2023"]),
    ("Audi", "Q5", ["2020", "2021", "2022"]),
    # Older vehicles (more likely to have recalls)
    ("Honda", "Accord", ["2012", "2013", "2014", "2015"]),
    ("Toyota", "Corolla", ["2012", "2013", "2014", "2015"]),
    ("Acura", "RDX", ["2012", "2013", "2014", "2015"]),
    ("Subaru", "Outback", ["2015", "2016", "2017", "2018"]),
]

# Question templates by query type
RECALL_QUESTIONS = [
    "Are there any recalls for a {year} {make} {model}?",
    "Is my {year} {make} {model} under any safety recalls?",
    "Check if there are recalls on a {year} {make} {model}",
    "Does the {year} {make} {model} have any open recalls?",
    "What recalls affect the {year} {make} {model}?",
    "I own a {year} {make} {model}. Are there any recalls I should know about?",
]

COMPLAINT_QUESTIONS = [
    "How many complaints have been filed for the {year} {make} {model}?",
    "What are common problems with the {year} {make} {model}?",
    "Are there any reported issues with the {year} {make} {model}?",
    "What do owners complain about on the {year} {make} {model}?",
]

SAFETY_RATING_QUESTIONS = [
    "What is the safety rating for a {year} {make} {model}?",
    "How does the {year} {make} {model} perform in crash tests?",
    "Is the {year} {make} {model} safe?",
    "What's the NHTSA rating for the {year} {make} {model}?",
    "How many stars did the {year} {make} {model} get in safety tests?",
]

COMPONENT_COMPLAINTS = [
    ("BRAKES", "brake", ["brake problems", "brake issues", "braking concerns"]),
    ("AIR BAGS", "airbag", ["airbag issues", "airbag problems", "airbag concerns"]),
    ("ENGINE", "engine", ["engine problems", "engine issues", "engine trouble"]),
    ("STEERING", "steering", ["steering problems", "steering issues"]),
    ("ELECTRICAL SYSTEM", "electrical", ["electrical problems", "electrical issues"]),
    ("SUSPENSION", "suspension", ["suspension problems", "suspension issues"]),
]

FEATURE_QUESTIONS = [
    ("NHTSAForwardCollisionWarning", "Does the {year} {make} {model} have forward collision warning?"),
    ("NHTSALaneDepartureWarning", "Is lane departure warning standard on the {year} {make} {model}?"),
    ("NHTSAElectronicStabilityControl", "Does the {year} {make} {model} come with electronic stability control?"),
]


def api_query(endpoint: str) -> dict | None:
    """Query the NHTSA API."""
    url = f"{API_BASE}{endpoint}"
    try:
        with urlopen(url, timeout=15) as resp:
            return json.loads(resp.read().decode())
    except (URLError, json.JSONDecodeError) as e:
        print(f"  ⚠️ API error for {endpoint}: {e}")
        return None


def create_example(question: str, query_type: str, **metadata) -> dict:
    """Create a training example."""
    return {
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": question}
        ],
        "query_type": query_type,
        **metadata
    }


def generate_recall_examples(vehicles: list, count: int = 50) -> list:
    """Generate recall query examples."""
    examples = []
    sampled = random.sample(vehicles * 2, min(count, len(vehicles) * 2))  # Allow duplicates

    print(f"\n📋 Generating {len(sampled)} recall examples...")
    for make, model, years in sampled:
        year = random.choice(years)
        question = random.choice(RECALL_QUESTIONS).format(year=year, make=make, model=model)
        examples.append(create_example(question, "recalls", make=make, model=model, year=year))

    return examples


def generate_complaint_examples(vehicles: list, count: int = 40) -> list:
    """Generate complaint query examples."""
    examples = []
    sampled = random.sample(vehicles * 2, min(count, len(vehicles) * 2))

    print(f"📋 Generating {len(sampled)} complaint examples...")
    for make, model, years in sampled:
        year = random.choice(years)
        question = random.choice(COMPLAINT_QUESTIONS).format(year=year, make=make, model=model)
        examples.append(create_example(question, "complaints", make=make, model=model, year=year))

    return examples


def generate_component_complaint_examples(vehicles: list, count: int = 30) -> list:
    """Generate component-specific complaint examples."""
    examples = []

    print(f"📋 Generating {count} component-specific complaint examples...")
    for _ in range(count):
        make, model, years = random.choice(vehicles)
        year = random.choice(years)
        component, keyword, phrases = random.choice(COMPONENT_COMPLAINTS)
        phrase = random.choice(phrases)

        templates = [
            f"Are there any {phrase} reported for the {year} {make} {model}?",
            f"Have owners reported {keyword} issues with the {year} {make} {model}?",
            f"What {keyword} complaints exist for the {year} {make} {model}?",
        ]
        question = random.choice(templates)
        examples.append(create_example(
            question, "complaints",
            make=make, model=model, year=year, component_filter=component
        ))

    return examples


def generate_safety_rating_examples(vehicles: list, count: int = 40) -> list:
    """Generate safety rating examples."""
    examples = []
    # Focus on newer vehicles that have ratings
    recent = [(m, mo, [y for y in yrs if int(y) >= 2020]) for m, mo, yrs in vehicles]
    recent = [(m, mo, yrs) for m, mo, yrs in recent if yrs]
    sampled = random.sample(recent * 2, min(count, len(recent) * 2))

    print(f"📋 Generating {len(sampled)} safety rating examples...")
    for make, model, years in sampled:
        year = random.choice(years)
        question = random.choice(SAFETY_RATING_QUESTIONS).format(year=year, make=make, model=model)
        examples.append(create_example(question, "safety_rating", make=make, model=model, year=year))

    return examples


def generate_specific_rating_examples(vehicles: list, count: int = 20) -> list:
    """Generate examples asking about specific ratings (rollover, front crash, etc.)."""
    examples = []
    rating_fields = [
        ("OverallFrontCrashRating", "front crash rating", "front crash test"),
        ("OverallSideCrashRating", "side crash rating", "side impact test"),
        ("RolloverRating", "rollover rating", "rollover test"),
    ]

    recent = [(m, mo, [y for y in yrs if int(y) >= 2020]) for m, mo, yrs in vehicles]
    recent = [(m, mo, yrs) for m, mo, yrs in recent if yrs]

    print(f"📋 Generating {count} specific rating examples...")
    for _ in range(count):
        make, model, years = random.choice(recent)
        year = random.choice(years)
        field, phrase1, phrase2 = random.choice(rating_fields)

        templates = [
            f"What is the {phrase1} for the {year} {make} {model}?",
            f"How did the {year} {make} {model} perform in the {phrase2}?",
        ]
        question = random.choice(templates)
        examples.append(create_example(
            question, "safety_rating",
            make=make, model=model, year=year, rating_field=field
        ))

    return examples


def generate_safety_feature_examples(vehicles: list, count: int = 20) -> list:
    """Generate safety feature examples."""
    examples = []
    recent = [(m, mo, [y for y in yrs if int(y) >= 2022]) for m, mo, yrs in vehicles]
    recent = [(m, mo, yrs) for m, mo, yrs in recent if yrs]

    print(f"📋 Generating {count} safety feature examples...")
    for _ in range(count):
        make, model, years = random.choice(recent)
        year = random.choice(years)
        feature, question_template = random.choice(FEATURE_QUESTIONS)
        question = question_template.format(year=year, make=make, model=model)
        examples.append(create_example(
            question, "safety_features",
            make=make, model=model, year=year, feature=feature
        ))

    return examples


def generate_comparison_examples(vehicles: list, count: int = 15) -> list:
    """Generate vehicle comparison examples."""
    examples = []
    recent = [(m, mo, [y for y in yrs if int(y) >= 2022]) for m, mo, yrs in vehicles]
    recent = [(m, mo, yrs) for m, mo, yrs in recent if yrs]

    print(f"📋 Generating {count} comparison examples...")
    for _ in range(count):
        v1 = random.choice(recent)
        v2 = random.choice([v for v in recent if v != v1])
        year = random.choice(list(set(v1[2]) & set(v2[2])) or v1[2])

        templates = [
            f"Compare the safety ratings of the {year} {v1[0]} {v1[1]} and {year} {v2[0]} {v2[1]}.",
            f"Which is safer: {year} {v1[0]} {v1[1]} or {year} {v2[0]} {v2[1]}?",
            f"How do the crash test ratings compare between {v1[0]} {v1[1]} and {v2[0]} {v2[1]}?",
        ]
        examples.append(create_example(
            random.choice(templates), "comparison",
            vehicles=[
                {"make": v1[0], "model": v1[1], "year": year},
                {"make": v2[0], "model": v2[1], "year": year}
            ]
        ))

    return examples



def generate_recall_count_examples(vehicles: list, count: int = 15) -> list:
    """Generate examples asking for recall counts."""
    examples = []
    sampled = random.sample(vehicles, min(count, len(vehicles)))

    print(f"📋 Generating {len(sampled)} recall count examples...")
    for make, model, years in sampled:
        year = random.choice(years)
        templates = [
            f"How many recalls does the {year} {make} {model} have?",
            f"What's the total number of recalls for a {year} {make} {model}?",
        ]
        examples.append(create_example(
            random.choice(templates), "recall_count",
            make=make, model=model, year=year
        ))

    return examples


def generate_complaint_count_examples(vehicles: list, count: int = 15) -> list:
    """Generate examples asking for complaint counts."""
    examples = []
    sampled = random.sample(vehicles, min(count, len(vehicles)))

    print(f"📋 Generating {len(sampled)} complaint count examples...")
    for make, model, years in sampled:
        year = random.choice(years)
        templates = [
            f"How many complaints have been filed for the {year} {make} {model}?",
            f"What's the complaint count for a {year} {make} {model}?",
        ]
        examples.append(create_example(
            random.choice(templates), "complaint_count",
            make=make, model=model, year=year
        ))

    return examples


def generate_validation_set(vehicles: list) -> list:
    """Generate a smaller validation set covering all query types."""
    examples = []

    # Use different seed for validation to avoid overlap
    random.seed(99)

    examples.extend(generate_recall_examples(vehicles, 10))
    examples.extend(generate_recall_count_examples(vehicles, 5))
    examples.extend(generate_complaint_examples(vehicles, 8))
    examples.extend(generate_component_complaint_examples(vehicles, 6))
    examples.extend(generate_complaint_count_examples(vehicles, 5))
    examples.extend(generate_safety_rating_examples(vehicles, 8))
    examples.extend(generate_specific_rating_examples(vehicles, 4))
    examples.extend(generate_safety_feature_examples(vehicles, 4))
    examples.extend(generate_comparison_examples(vehicles, 5))

    random.shuffle(examples)
    return examples


def main():
    """Generate expanded training and validation datasets."""
    print("🚗 NHTSA Training Data Generator")
    print("=" * 50)

    # Generate training set
    random.seed(42)
    all_examples = []
    all_examples.extend(generate_recall_examples(VEHICLES, 50))
    all_examples.extend(generate_recall_count_examples(VEHICLES, 15))
    all_examples.extend(generate_complaint_examples(VEHICLES, 40))
    all_examples.extend(generate_component_complaint_examples(VEHICLES, 30))
    all_examples.extend(generate_complaint_count_examples(VEHICLES, 15))
    all_examples.extend(generate_safety_rating_examples(VEHICLES, 40))
    all_examples.extend(generate_specific_rating_examples(VEHICLES, 20))
    all_examples.extend(generate_safety_feature_examples(VEHICLES, 20))
    all_examples.extend(generate_comparison_examples(VEHICLES, 15))
    random.shuffle(all_examples)

    with open(OUTPUT_FILE, "w") as f:
        for ex in all_examples:
            f.write(json.dumps(ex) + "\n")

    print(f"\n✅ Generated {len(all_examples)} training examples")
    print(f"📁 Saved to: {OUTPUT_FILE}")

    # Generate validation set
    print("\n" + "-" * 50)
    print("Generating validation set...")
    val_examples = generate_validation_set(VEHICLES)

    with open(VALIDATION_FILE, "w") as f:
        for ex in val_examples:
            f.write(json.dumps(ex) + "\n")

    print(f"✅ Generated {len(val_examples)} validation examples")
    print(f"📁 Saved to: {VALIDATION_FILE}")

    # Summary
    print("\n" + "=" * 50)
    print("📊 Training breakdown:")
    by_type = {}
    for ex in all_examples:
        qt = ex.get("query_type", "unknown")
        by_type[qt] = by_type.get(qt, 0) + 1
    for qt, count in sorted(by_type.items(), key=lambda x: -x[1]):
        print(f"   {qt}: {count}")

    print("\n📊 Validation breakdown:")
    by_type = {}
    for ex in val_examples:
        qt = ex.get("query_type", "unknown")
        by_type[qt] = by_type.get(qt, 0) + 1
    for qt, count in sorted(by_type.items(), key=lambda x: -x[1]):
        print(f"   {qt}: {count}")


if __name__ == "__main__":
    main()

