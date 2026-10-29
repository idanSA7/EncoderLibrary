import numpy as np

def gaussian_kernel(distances: np.ndarray, bandwidth: float) -> np.ndarray:

    if bandwidth <= 0.0:
        raise ValueError("Bandwidth must be strictly positive.")

    normalized_distances = distances / bandwidth
    weights = np.exp(-0.5 * (normalized_distances ** 2))
    return weights


def compute_euclidean_distances(point: np.ndarray, data: np.ndarray) -> np.ndarray:
    return np.linalg.norm(data - point, axis=1)


def compute_weighted_centroid(point: np.ndarray, data: np.ndarray, bandwidth: float) -> np.ndarray:

    distances = compute_euclidean_distances(point, data)
    weights = gaussian_kernel(distances, bandwidth)
    sum_weights = np.sum(weights)
    if sum_weights == 0.0:
        return point.copy()

    weighted_data = data * weights[:, np.newaxis]
    numerator = np.sum(weighted_data, axis=0)
    return numerator / sum_weights