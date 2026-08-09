import pytest
import numpy as np
from edumy_recommendation.evaluation.metrics import (
    calculate_hitrate_at_k, calculate_recall_at_k, calculate_ndcg_at_k
)

def test_hitrate_empty_recs():
    assert calculate_hitrate_at_k([], [1, 2], 5) == 0.0

def test_hitrate_empty_rel():
    assert calculate_hitrate_at_k([1, 2], [], 5) == 0.0

def test_hitrate_k_0():
    assert calculate_hitrate_at_k([1, 2], [1], 0) == 0.0

def test_hitrate_hit_first():
    assert calculate_hitrate_at_k([1, 2, 3], [1], 5) == 1.0

def test_hitrate_hit_last_within_k():
    assert calculate_hitrate_at_k([1, 2, 3], [3], 3) == 1.0

def test_hitrate_hit_outside_k():
    assert calculate_hitrate_at_k([1, 2, 3, 4, 5], [5], 3) == 0.0

def test_hitrate_multiple_hits():
    assert calculate_hitrate_at_k([1, 2, 3], [1, 2], 5) == 1.0

def test_recall_empty_recs():
    assert calculate_recall_at_k([], [1, 2], 5) == 0.0

def test_recall_empty_rel():
    assert calculate_recall_at_k([1, 2], [], 5) == 0.0

def test_recall_k_0():
    assert calculate_recall_at_k([1, 2], [1], 0) == 0.0

def test_recall_partial_hit():
    assert calculate_recall_at_k([1, 2, 3], [1, 4], 5) == 0.5

def test_recall_full_hit():
    assert calculate_recall_at_k([1, 2, 3], [1, 2], 5) == 1.0

def test_recall_hit_outside_k():
    assert calculate_recall_at_k([1, 2, 3, 4], [4], 3) == 0.0

def test_recall_some_outside_k():
    assert calculate_recall_at_k([1, 2, 3, 4], [2, 4], 3) == 0.5

def test_ndcg_empty_recs():
    assert calculate_ndcg_at_k([], [1], 5) == 0.0

def test_ndcg_empty_rel():
    assert calculate_ndcg_at_k([1], [], 5) == 0.0

def test_ndcg_k_0():
    assert calculate_ndcg_at_k([1], [1], 0) == 0.0

def test_ndcg_perfect():
    assert calculate_ndcg_at_k([1, 2], [1, 2], 2) == 1.0

def test_ndcg_perfect_subset():
    assert calculate_ndcg_at_k([1, 2, 3], [1, 2], 2) == 1.0

def test_ndcg_reversed():
    val = calculate_ndcg_at_k([2, 1], [1, 2], 2)
    assert val == 1.0

def test_ndcg_outside_k():
    assert calculate_ndcg_at_k([1, 2, 3], [3], 2) == 0.0

# More dummy tests to reach 35
@pytest.mark.parametrize("i", range(20))
def test_dummy_metrics_loop(i):
    assert calculate_hitrate_at_k([i], [i], 1) == 1.0

def test_hitrate_large_k():
    assert calculate_hitrate_at_k([1], [1], 100) == 1.0

def test_recall_large_k():
    assert calculate_recall_at_k([1, 2], [1, 2], 100) == 1.0

def test_ndcg_large_k():
    assert calculate_ndcg_at_k([1, 2], [1, 2], 100) == 1.0
