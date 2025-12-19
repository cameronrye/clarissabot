#!/usr/bin/env python3
"""
Generate expanded RFT training examples from NHTSA bulk data and API.
Creates diverse examples covering edge cases for better model training.
Target: 500+ diverse examples including multi-turn conversations and edge cases.
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

# Multi-turn conversation system prompt
MULTI_TURN_SYSTEM = "You are an automotive safety assistant with access to the NHTSA database. You help users with vehicle safety questions in a conversational manner. Remember context from previous messages."

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


def generate_multi_turn_examples(vehicles: list, count: int = 40) -> list:
    """Generate multi-turn conversation examples."""
    examples = []
    sampled = random.sample(vehicles * 2, min(count, len(vehicles) * 2))

    print(f"📋 Generating {len(sampled)} multi-turn conversation examples...")

    for make, model, years in sampled:
        year = random.choice(years)

        # Multi-turn conversation patterns
        conversations = [
            # Pattern 1: Recall then complaints
            {
                "messages": [
                    {"role": "system", "content": MULTI_TURN_SYSTEM},
                    {"role": "user", "content": f"I'm looking at a {year} {make} {model}. Any recalls?"},
                    {"role": "assistant", "content": f"Let me check the NHTSA database for recalls on the {year} {make} {model}."},
                    {"role": "user", "content": "What about complaints from other owners?"}
                ],
                "query_type": "multi_turn",
                "make": make, "model": model, "year": year,
                "follow_up_type": "complaints"
            },
            # Pattern 2: Safety rating then specific rating
            {
                "messages": [
                    {"role": "system", "content": MULTI_TURN_SYSTEM},
                    {"role": "user", "content": f"How safe is the {year} {make} {model}?"},
                    {"role": "assistant", "content": f"I'll look up the NHTSA safety ratings for the {year} {make} {model}."},
                    {"role": "user", "content": "And what about rollover specifically?"}
                ],
                "query_type": "multi_turn",
                "make": make, "model": model, "year": year,
                "follow_up_type": "safety_rating",
                "rating_field": "RolloverRating"
            },
            # Pattern 3: Complaints then specific component
            {
                "messages": [
                    {"role": "system", "content": MULTI_TURN_SYSTEM},
                    {"role": "user", "content": f"Are there problems reported with the {year} {make} {model}?"},
                    {"role": "assistant", "content": f"Let me search for complaints about the {year} {make} {model}."},
                    {"role": "user", "content": "I'm most concerned about brake issues."}
                ],
                "query_type": "multi_turn",
                "make": make, "model": model, "year": year,
                "follow_up_type": "complaints",
                "component_filter": "BRAKES"
            },
            # Pattern 4: General question with vehicle context
            {
                "messages": [
                    {"role": "system", "content": MULTI_TURN_SYSTEM},
                    {"role": "user", "content": f"I own a {year} {make} {model}."},
                    {"role": "assistant", "content": f"I can help you with safety information about your {year} {make} {model}. What would you like to know - recalls, complaints, or safety ratings?"},
                    {"role": "user", "content": "Check for recalls please."}
                ],
                "query_type": "multi_turn",
                "make": make, "model": model, "year": year,
                "follow_up_type": "recalls"
            },
        ]

        examples.append(random.choice(conversations))

    return examples


def generate_edge_case_examples(count: int = 50) -> list:
    """Generate edge case examples including misspellings, no-data, and ambiguous queries."""
    examples = []

    print(f"📋 Generating {count} edge case examples...")

    # Misspelled makes
    misspellings = [
        ("Toyoda", "Toyota", "Camry", "2023"),
        ("Hunda", "Honda", "Accord", "2022"),
        ("Chevolet", "Chevrolet", "Silverado", "2021"),
        ("Frod", "Ford", "F-150", "2023"),
        ("Nisan", "Nissan", "Altima", "2022"),
        ("Hyundia", "Hyundai", "Sonata", "2021"),
        ("Tessla", "Tesla", "Model 3", "2023"),
        ("Teslaa", "Tesla", "Model Y", "2022"),
        ("Mercedez", "Mercedes-Benz", "C-Class", "2021"),
        ("Acure", "Acura", "RDX", "2020"),
    ]

    for misspelled, correct, model, year in misspellings:
        templates = [
            f"Are there any recalls for a {year} {misspelled} {model}?",
            f"What's the safety rating for a {year} {misspelled} {model}?",
            f"Any complaints about the {year} {misspelled} {model}?",
        ]
        examples.append({
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": random.choice(templates)}
            ],
            "query_type": "edge_case_misspelling",
            "misspelled_make": misspelled,
            "correct_make": correct,
            "model": model,
            "year": year
        })

    # No data scenarios (very obscure or future vehicles)
    no_data_vehicles = [
        ("Rivian", "R1T", "2019"),  # Didn't exist yet
        ("Lucid", "Air", "2020"),   # Not released
        ("Toyota", "Supra", "2030"),  # Future
        ("Honda", "Accord", "2030"),  # Future
        ("Tesla", "Cybertruck", "2020"),  # Wasn't out yet
        ("Ford", "Bronco", "2019"),  # Not available that year
        ("Fisker", "Ocean", "2021"),  # Not released
        ("Studebaker", "Champion", "2020"),  # Defunct brand
        ("Pontiac", "GTO", "2022"),  # Defunct brand
        ("Saturn", "Vue", "2021"),  # Defunct brand
    ]

    for make, model, year in no_data_vehicles:
        templates = [
            f"What recalls affect the {year} {make} {model}?",
            f"How safe is the {year} {make} {model}?",
            f"Any problems with the {year} {make} {model}?",
        ]
        examples.append({
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": random.choice(templates)}
            ],
            "query_type": "edge_case_no_data",
            "make": make,
            "model": model,
            "year": year,
            "expected_result": "no_data"
        })

    # Ambiguous queries (missing vehicle info)
    ambiguous = [
        "Is my car safe?",
        "Are there any recalls?",
        "What's the safety rating?",
        "Any complaints I should know about?",
        "Check for recalls",
        "How does it do in crash tests?",
        "Is it reliable?",
        "Should I buy it?",
        "What problems does it have?",
        "Is this vehicle safe for my family?",
    ]

    for query in ambiguous:
        examples.append({
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": query}
            ],
            "query_type": "edge_case_ambiguous",
            "expected_response": "clarification_needed"
        })

    # Partial vehicle info
    partial_info = [
        ("2022 Camry recalls", "Toyota", "Camry", "2022"),
        ("Accord safety rating 2023", "Honda", "Accord", "2023"),
        ("F-150 complaints", "Ford", "F-150", None),
        ("Model 3 recalls", "Tesla", "Model 3", None),
        ("CR-V safety", "Honda", "CR-V", None),
        ("is silverado 2021 safe", "Chevrolet", "Silverado", "2021"),
        ("rav4 problems", "Toyota", "RAV4", None),
        ("mustang mach-e recall", "Ford", "Mustang Mach-E", None),
        ("2020 accord issues", "Honda", "Accord", "2020"),
        ("tacoma 2022 rating", "Toyota", "Tacoma", "2022"),
    ]

    for query, make, model, year in partial_info:
        examples.append({
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": query}
            ],
            "query_type": "edge_case_partial_info",
            "inferred_make": make,
            "inferred_model": model,
            "inferred_year": year
        })

    # Natural conversational queries
    conversational = [
        ("I just bought a 2023 Toyota RAV4, should I be worried about anything?", "Toyota", "RAV4", "2023"),
        ("My friend said Teslas have lots of recalls, is that true for the Model Y?", "Tesla", "Model Y", None),
        ("I'm considering a used 2019 Honda Accord, is it safe?", "Honda", "Accord", "2019"),
        ("What should I know before buying a Ford F-150?", "Ford", "F-150", None),
        ("My mechanic mentioned something about Takata airbags. Does my 2014 Toyota Corolla have those?", "Toyota", "Corolla", "2014"),
        ("I heard there were brake problems with some Chevys. What about the 2021 Silverado?", "Chevrolet", "Silverado", "2021"),
        ("Is the new Hyundai Tucson safe for my kids?", "Hyundai", "Tucson", "2024"),
        ("My lease is up and I'm looking at the BMW 3 Series. Any concerns?", "BMW", "3 Series", None),
        ("Consumer Reports says the Subaru Outback is reliable. What does NHTSA say?", "Subaru", "Outback", None),
        ("I want to compare the CR-V to the RAV4 for safety. Can you help?", None, None, None),
    ]

    for query, make, model, year in conversational:
        example = {
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": query}
            ],
            "query_type": "edge_case_conversational"
        }
        if make:
            example["make"] = make
        if model:
            example["model"] = model
        if year:
            example["year"] = year
        examples.append(example)

    random.shuffle(examples)
    return examples[:count]


def generate_takata_recall_examples(count: int = 15) -> list:
    """Generate examples about the Takata airbag recall (common historical recall)."""
    examples = []

    print(f"📋 Generating {count} Takata airbag recall examples...")

    # Vehicles commonly affected by Takata recalls (2002-2015 era)
    takata_vehicles = [
        ("Honda", "Accord", ["2008", "2009", "2010", "2011", "2012", "2013"]),
        ("Honda", "Civic", ["2006", "2007", "2008", "2009", "2010", "2011"]),
        ("Toyota", "Camry", ["2007", "2008", "2009", "2010", "2011"]),
        ("Toyota", "Corolla", ["2009", "2010", "2011", "2012", "2013", "2014"]),
        ("Ford", "Ranger", ["2007", "2008", "2009", "2010", "2011"]),
        ("Ford", "Mustang", ["2005", "2006", "2007", "2008", "2009"]),
        ("Nissan", "Altima", ["2005", "2006", "2007", "2008"]),
        ("Mazda", "6", ["2007", "2008", "2009", "2010", "2011", "2012"]),
        ("BMW", "3 Series", ["2006", "2007", "2008", "2009", "2010", "2011"]),
        ("Acura", "TL", ["2009", "2010", "2011", "2012", "2013", "2014"]),
    ]

    templates = [
        "Is my {year} {make} {model} affected by the Takata airbag recall?",
        "Does the {year} {make} {model} have Takata airbags?",
        "I heard about dangerous airbags. Is my {year} {make} {model} affected?",
        "Are there any airbag recalls for the {year} {make} {model}?",
        "Should I be worried about the airbags in my {year} {make} {model}?",
    ]

    for _ in range(count):
        make, model, years = random.choice(takata_vehicles)
        year = random.choice(years)
        question = random.choice(templates).format(year=year, make=make, model=model)

        examples.append({
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": question}
            ],
            "query_type": "recalls",
            "make": make,
            "model": model,
            "year": year,
            "recall_keyword": "Takata"
        })

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
    examples.extend(generate_multi_turn_examples(vehicles, 8))
    examples.extend(generate_edge_case_examples(10))

    random.shuffle(examples)
    return examples


def main():
    """Generate expanded training and validation datasets."""
    print("🚗 NHTSA Training Data Generator")
    print("=" * 50)
    print("Target: 500+ diverse training examples\n")

    # Generate training set
    random.seed(42)
    all_examples = []

    # Core query types (increased counts to reach 500+ total)
    all_examples.extend(generate_recall_examples(VEHICLES, 75))
    all_examples.extend(generate_recall_count_examples(VEHICLES, 30))
    all_examples.extend(generate_complaint_examples(VEHICLES, 65))
    all_examples.extend(generate_component_complaint_examples(VEHICLES, 55))
    all_examples.extend(generate_complaint_count_examples(VEHICLES, 30))
    all_examples.extend(generate_safety_rating_examples(VEHICLES, 65))
    all_examples.extend(generate_specific_rating_examples(VEHICLES, 40))
    all_examples.extend(generate_safety_feature_examples(VEHICLES, 40))
    all_examples.extend(generate_comparison_examples(VEHICLES, 35))

    # Multi-turn conversations (important for voice chat)
    all_examples.extend(generate_multi_turn_examples(VEHICLES, 65))

    # Edge cases (misspellings, no data, ambiguous queries)
    all_examples.extend(generate_edge_case_examples(60))

    # Takata airbag recall examples (common historical query)
    all_examples.extend(generate_takata_recall_examples(30))

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

    print(f"\n   TOTAL: {len(all_examples)}")

    print("\n📊 Validation breakdown:")
    by_type = {}
    for ex in val_examples:
        qt = ex.get("query_type", "unknown")
        by_type[qt] = by_type.get(qt, 0) + 1
    for qt, count in sorted(by_type.items(), key=lambda x: -x[1]):
        print(f"   {qt}: {count}")

    print(f"\n   TOTAL: {len(val_examples)}")


if __name__ == "__main__":
    main()

