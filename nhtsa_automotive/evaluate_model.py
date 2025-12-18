#!/usr/bin/env python3
"""
Evaluate model performance on NHTSA validation set.
Compares base model vs fine-tuned model accuracy using the grader.

Usage:
    python evaluate_model.py [--model MODEL_NAME] [--samples N] [--verbose]
    
Examples:
    python evaluate_model.py --model gpt-4o-mini --samples 20
    python evaluate_model.py --model nhtsa-safety-v1 --verbose
"""

import argparse
import json
import os
import sys
from pathlib import Path
from typing import Optional

# Add graders to path
sys.path.insert(0, str(Path(__file__).parent / "graders"))
from nhtsa_grader import grade

# Try to import Azure OpenAI client
try:
    from openai import AzureOpenAI
    OPENAI_AVAILABLE = True
except ImportError:
    OPENAI_AVAILABLE = False
    print("⚠️  openai package not installed. Install with: pip install openai")


SCRIPT_DIR = Path(__file__).parent
VALIDATION_FILE = SCRIPT_DIR / "rft_validation_expanded.jsonl"

SYSTEM_PROMPT = """You are an automotive safety assistant with access to the NHTSA database. 
Answer questions about vehicle recalls, complaints, and safety ratings accurately and concisely."""


def load_validation_set(limit: Optional[int] = None) -> list[dict]:
    """Load validation examples from JSONL file."""
    examples = []
    with open(VALIDATION_FILE) as f:
        for line in f:
            if line.strip():
                examples.append(json.loads(line))
    if limit:
        examples = examples[:limit]
    return examples


def get_model_response(client: "AzureOpenAI", model: str, messages: list[dict]) -> str:
    """Get response from Azure OpenAI model."""
    try:
        response = client.chat.completions.create(
            model=model,
            messages=messages,
            temperature=0,
            max_tokens=500
        )
        return response.choices[0].message.content or ""
    except Exception as e:
        print(f"  ❌ API Error: {e}")
        return ""


def evaluate_example(client: "AzureOpenAI", model: str, example: dict, verbose: bool = False) -> dict:
    """Evaluate a single example and return results."""
    messages = example.get("messages", [])
    query_type = example.get("query_type", "unknown")
    
    # Get model response
    response_text = get_model_response(client, model, messages)
    
    # Grade the response
    sample = {"output_text": response_text}
    score = grade(example, sample)
    
    result = {
        "query_type": query_type,
        "score": score,
        "passed": score >= 0.7,
        "response": response_text[:100] + "..." if len(response_text) > 100 else response_text
    }
    
    if verbose:
        user_msg = messages[-1]["content"] if messages else "N/A"
        status = "✅" if result["passed"] else "❌"
        print(f"{status} [{query_type}] Score: {score:.2f}")
        print(f"   Q: {user_msg[:60]}...")
        print(f"   A: {result['response'][:60]}...")
        print()
    
    return result


def run_evaluation(model: str, samples: Optional[int] = None, verbose: bool = False):
    """Run full evaluation on validation set."""
    if not OPENAI_AVAILABLE:
        print("⚠️  openai package not available - running dry run mode")
        dry_run_evaluation(samples, verbose)
        return
    
    # Check for Azure OpenAI configuration
    endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    api_key = os.getenv("AZURE_OPENAI_KEY")
    
    if not endpoint or not api_key:
        print("❌ Missing Azure OpenAI configuration.")
        print("   Set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_KEY environment variables.")
        print("\n📋 Running in DRY RUN mode (grader only, no model calls)")
        dry_run_evaluation(samples, verbose)
        return
    
    # Initialize client
    client = AzureOpenAI(
        azure_endpoint=endpoint,
        api_key=api_key,
        api_version="2024-10-01-preview"
    )
    
    # Load validation set
    examples = load_validation_set(samples)
    print(f"🚗 NHTSA Model Evaluation")
    print(f"Model: {model}")
    print(f"Examples: {len(examples)}")
    print("=" * 60)
    
    results = []
    for i, example in enumerate(examples):
        if not verbose:
            print(f"  Evaluating {i+1}/{len(examples)}...", end="\r")
        result = evaluate_example(client, model, example, verbose)
        results.append(result)
    
    # Calculate metrics
    print_summary(results)


def dry_run_evaluation(samples: Optional[int] = None, verbose: bool = False):
    """Run evaluation without model calls (test grader only)."""
    examples = load_validation_set(samples)
    print(f"📋 Dry Run Evaluation (Grader Test)")
    print(f"Examples: {len(examples)}")
    print("=" * 60)
    
    # Simulate with dummy responses
    by_type: dict[str, list[float]] = {}
    for example in examples:
        qt = example.get("query_type", "unknown")
        # Simulate a neutral score for dry run
        score = 0.5
        by_type.setdefault(qt, []).append(score)
    
    print("\n📊 Query Type Distribution:")
    for qt, scores in sorted(by_type.items(), key=lambda x: -len(x[1])):
        print(f"   {qt}: {len(scores)} examples")
    print(f"\n   Total: {len(examples)} examples ready for evaluation")


def print_summary(results: list[dict]):
    """Print evaluation summary."""
    total = len(results)
    passed = sum(1 for r in results if r["passed"])
    avg_score = sum(r["score"] for r in results) / total if total else 0

    print("\n" + "=" * 60)
    print("📊 EVALUATION RESULTS")
    print("=" * 60)

    # By query type
    by_type: dict[str, list[float]] = {}
    for r in results:
        by_type.setdefault(r["query_type"], []).append(r["score"])

    print("\nBy Query Type:")
    for qt, scores in sorted(by_type.items(), key=lambda x: -sum(x[1])/len(x[1])):
        avg = sum(scores) / len(scores)
        pass_rate = sum(1 for s in scores if s >= 0.7) / len(scores) * 100
        print(f"   {qt}: {avg:.2f} avg ({pass_rate:.0f}% pass rate, n={len(scores)})")

    print("\n" + "-" * 60)
    print(f"Overall Average Score: {avg_score:.2f}")
    print(f"Pass Rate: {passed}/{total} ({passed/total*100:.1f}%)")

    if avg_score >= 0.8:
        print("\n🎉 Excellent performance!")
    elif avg_score >= 0.7:
        print("\n✅ Good performance - meets threshold")
    elif avg_score >= 0.5:
        print("\n⚠️  Moderate performance - needs improvement")
    else:
        print("\n❌ Poor performance - review training data")


def compare_models(base_model: str, finetuned_model: str, samples: int = 20):
    """Compare base model vs fine-tuned model performance."""
    if not OPENAI_AVAILABLE:
        print("❌ Cannot run comparison without openai package")
        return

    endpoint = os.getenv("AZURE_OPENAI_ENDPOINT")
    api_key = os.getenv("AZURE_OPENAI_KEY")

    if not endpoint or not api_key:
        print("❌ Missing Azure OpenAI configuration.")
        return

    client = AzureOpenAI(
        azure_endpoint=endpoint,
        api_key=api_key,
        api_version="2024-10-01-preview"
    )

    examples = load_validation_set(samples)
    print(f"🔄 Model Comparison")
    print(f"Base: {base_model}")
    print(f"Fine-tuned: {finetuned_model}")
    print(f"Examples: {len(examples)}")
    print("=" * 60)

    base_results = []
    ft_results = []

    for i, example in enumerate(examples):
        print(f"  Evaluating {i+1}/{len(examples)}...", end="\r")
        base_results.append(evaluate_example(client, base_model, example))
        ft_results.append(evaluate_example(client, finetuned_model, example))

    print("\n\n📊 COMPARISON RESULTS")
    print("=" * 60)

    base_avg = sum(r["score"] for r in base_results) / len(base_results)
    ft_avg = sum(r["score"] for r in ft_results) / len(ft_results)

    print(f"\nBase Model ({base_model}):")
    print(f"   Average Score: {base_avg:.2f}")
    print(f"   Pass Rate: {sum(1 for r in base_results if r['passed'])}/{len(base_results)}")

    print(f"\nFine-tuned Model ({finetuned_model}):")
    print(f"   Average Score: {ft_avg:.2f}")
    print(f"   Pass Rate: {sum(1 for r in ft_results if r['passed'])}/{len(ft_results)}")

    improvement = ft_avg - base_avg
    print(f"\n📈 Improvement: {improvement:+.2f} ({improvement/base_avg*100:+.1f}%)")


def main():
    parser = argparse.ArgumentParser(description="Evaluate NHTSA model performance")
    parser.add_argument("--model", default="gpt-4o-mini", help="Model to evaluate")
    parser.add_argument("--samples", type=int, help="Number of samples to evaluate")
    parser.add_argument("--verbose", action="store_true", help="Show detailed output")
    parser.add_argument("--compare", help="Compare with fine-tuned model")

    args = parser.parse_args()

    if args.compare:
        compare_models(args.model, args.compare, args.samples or 20)
    else:
        run_evaluation(args.model, args.samples, args.verbose)


if __name__ == "__main__":
    main()

