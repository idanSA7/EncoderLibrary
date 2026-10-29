import numpy as np
from app.core.config import DEFAULT_KRIGING_CONFIG

VARIOGRAM_LINEAR_COEFF = 1.5
VARIOGRAM_CUBIC_COEFF = 0.5
VARIOGRAM_EXPONENT = 3
MIN_SAMPLES_FOR_VARIANCE = 2
LAGRANGE_WEIGHT_CONSTRAINT = 1.0


def spherical_variogram(
        h: float,
        a: float = DEFAULT_KRIGING_CONFIG.default_range_a,
        c: float = DEFAULT_KRIGING_CONFIG.default_sill_c,
) -> float:
    if h <= 0.0:
        return 0.0
    elif h >= a:
        return c
    else:
        return c * (
                VARIOGRAM_LINEAR_COEFF * (h / a)
                - VARIOGRAM_CUBIC_COEFF * ((h / a) ** VARIOGRAM_EXPONENT)
        )


def calculate_sill(
        known_deltas: np.ndarray,
        default_sill: float = DEFAULT_KRIGING_CONFIG.default_sill_c,
) -> float:
    n_samples = len(known_deltas)
    if n_samples < MIN_SAMPLES_FOR_VARIANCE:
        return default_sill

    mean_delta = float(np.mean(known_deltas))
    sample_variance = float(np.sum((known_deltas - mean_delta) ** 2) / (n_samples - 1))

    return max(sample_variance, DEFAULT_KRIGING_CONFIG.min_variance_floor)


def euclidean_distance(known_coords: np.ndarray, row_idx: int, col_idx: int) -> float:
    return float(np.linalg.norm(known_coords[row_idx] - known_coords[col_idx]))


def build_kriging_matrix(
        known_coords: np.ndarray,
        a: float = DEFAULT_KRIGING_CONFIG.default_range_a,
        c: float = DEFAULT_KRIGING_CONFIG.default_sill_c,
) -> np.ndarray:
    n_samples = len(known_coords)
    kriging_matrix = np.zeros((n_samples + 1, n_samples + 1), dtype=float)

    for row_idx in range(n_samples):
        for col_idx in range(n_samples):
            if row_idx != col_idx:
                dist = euclidean_distance(known_coords, row_idx, col_idx)
                kriging_matrix[row_idx, col_idx] = spherical_variogram(dist, a, c)

    for idx in range(n_samples):
        kriging_matrix[n_samples, idx] = LAGRANGE_WEIGHT_CONSTRAINT
        kriging_matrix[idx, n_samples] = LAGRANGE_WEIGHT_CONSTRAINT

    return kriging_matrix


def build_kriging_vector(
        known_coords: np.ndarray,
        target_coord: np.ndarray,
        a: float = DEFAULT_KRIGING_CONFIG.default_range_a,
        c: float = DEFAULT_KRIGING_CONFIG.default_sill_c,
) -> np.ndarray:
    n_samples = len(known_coords)
    kriging_vector = np.zeros(n_samples + 1, dtype=float)

    for row_idx in range(n_samples):
        dist = float(np.linalg.norm(known_coords[row_idx] - target_coord))
        kriging_vector[row_idx] = spherical_variogram(dist, a, c)

    kriging_vector[n_samples] = LAGRANGE_WEIGHT_CONSTRAINT
    return kriging_vector

#checking the math functions:
def solve_kriging(
        known_coords: np.ndarray,
        known_deltas: np.ndarray,
        target_coord: np.ndarray,
        a: float = DEFAULT_KRIGING_CONFIG.default_range_a,
        c: float | None = None,
) -> tuple[float, float]:
    if c is None:
        c = calculate_sill(known_deltas, DEFAULT_KRIGING_CONFIG.default_sill_c)

    kriging_matrix = build_kriging_matrix(known_coords, a, c)
    kriging_vector = build_kriging_vector(known_coords, target_coord, a, c)
    weights = np.linalg.solve(kriging_matrix, kriging_vector)

    n_samples = len(known_deltas)
    sample_weights = weights[:n_samples]

    predicted_delta = float(np.sum(sample_weights * known_deltas))
    kriging_variance = float(np.sum(weights * kriging_vector))

    return predicted_delta, max(kriging_variance, 0.0)

if __name__ == "__main__":
    coords = np.array([
        [32.0, 34.8, 10.0],
        [32.0, 35.2, 10.0],
        [32.4, 34.8, 10.0],
        [32.4, 35.2, 10.0],
    ])
    deltas = np.array([2.0, 4.0, 2.0, 4.0])

    # Test 1: Exact sample point
    pred_exact, var_exact = solve_kriging(coords, deltas, coords[0])
    print("--- Test 1: Exact Sample Point ---")
    print(f"Expected: 2.0 | Predicted: {pred_exact:.4f}")
    print(f"Variance: {var_exact:.6f}\n")

    # Test 2: Geometric center
    center_coord = np.array([32.2, 35.0, 10.0])
    pred_center, var_center = solve_kriging(coords, deltas, center_coord)
    print("--- Test 2: Center Point ---")
    print(f"Expected: ~3.0 | Predicted: {pred_center:.4f}")
    print(f"Variance: {var_center:.6f}\n")

    assert np.isclose(pred_center, 3.0, atol=0.2), "Center point prediction failed"
    print("All tests passed successfully.")