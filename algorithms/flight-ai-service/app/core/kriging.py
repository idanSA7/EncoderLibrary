import numpy as np


def spherical_variogram(h: float, a: float = 40.0, c: float = 0.5) -> float:
    if h <= 0.0:
        return 0.0
    elif a <= h:
        return c
    else:
        return c * (1.5 * (h / a) - 0.5 * pow((h / a), 3))


def calculate_sill(known_deltas: np.ndarray, default_sill: float = 0.5) -> float:
    n_samples = len(known_deltas)
    if n_samples < 2:
        return default_sill

    sum_deltas = 0.0
    for delta in known_deltas:
        sum_deltas += delta
    mean_delta = sum_deltas / n_samples

    sum_squared_deviations = 0.0
    for delta in known_deltas:
        deviation = delta - mean_delta
        sum_squared_deviations += deviation**2
    sample_variance = sum_squared_deviations / (n_samples - 1)

    return max(sample_variance, 1e-4)

def build_kriging_matrix(
    known_coords: np.ndarray, a: float = 40.0, c: float = 0.5) -> np.ndarray:
    n_samples = len(known_coords)

    kriging_matrix = np.zeros((n_samples + 1, n_samples + 1), dtype=float)

    for row_idx in range(n_samples):
        for col_idx in range(n_samples):
            if row_idx != col_idx:
                euclidean_distance = float(
                    np.linalg.norm(
                        known_coords[row_idx] - known_coords[col_idx]))
                kriging_matrix[row_idx, col_idx] = spherical_variogram(euclidean_distance, a, c)

    for idx in range(n_samples):
        kriging_matrix[n_samples, idx] = 1.0
        kriging_matrix[idx, n_samples] = 1.0

    return kriging_matrix


