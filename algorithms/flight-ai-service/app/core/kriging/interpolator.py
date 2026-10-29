import numpy as np
from app.core.kriging import math as kriging
from app.core.config import KrigingConfig, DEFAULT_KRIGING_CONFIG


class OrdinaryKriging:
    def __init__(self, config: KrigingConfig = DEFAULT_KRIGING_CONFIG):
        self.config = config

        self._coords: np.ndarray | None = None
        self._deltas: np.ndarray | None = None
        self._sill: float | None = None
        self._kriging_matrix: np.ndarray | None = None

    @property
    def is_fitted(self) -> bool:
        return self._coords is not None and self._kriging_matrix is not None

    def fit(self, known_coords: np.ndarray, known_deltas: np.ndarray) -> None:
        self._coords = known_coords
        self._deltas = known_deltas

        self._sill = kriging.calculate_sill(
            known_deltas,
            default_sill=self.config.default_sill_c
        )
        self._kriging_matrix = kriging.build_kriging_matrix(
            known_coords,
            a=self.config.default_range_a,
            c=self._sill
        )

    def predict(self, target_coord: np.ndarray) -> tuple[float, float]:
        if not self.is_fitted or self._coords is None or self._deltas is None or self._sill is None:
            raise RuntimeError("Cannot call predict on an unfitted model. Call fit() first.")

        kriging_vector = kriging.build_kriging_vector(
            self._coords,
            target_coord,
            a=self.config.default_range_a,
            c=self._sill,
        )

        weights = np.linalg.solve(self._kriging_matrix, kriging_vector)

        n_samples = len(self._deltas)
        sample_weights = weights[:n_samples]

        predicted_delta = float(np.sum(sample_weights * self._deltas))
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

    model = OrdinaryKriging()

    # Test 1: Predict before fit must raise RuntimeError
    try:
        model.predict(np.array([32.1, 34.9, 10.0]))
        assert False, "Expected RuntimeError when predicting before fit"
    except RuntimeError:
        print("--- Test 1: Unfitted Guard Check ---")
        print("Success: Predict before fit raised RuntimeError as expected.\n")

    # Fit the model
    model.fit(coords, deltas)

    # Test 2: Exact sample point
    pred_exact, var_exact = model.predict(coords[0])
    print("--- Test 2: Exact Sample Point ---")
    print(f"Expected: 2.0 | Predicted: {pred_exact:.4f}")
    print(f"Variance: {var_exact:.6f}\n")

    # Test 3: Geometric center
    center_coord = np.array([32.2, 35.0, 10.0])
    pred_center, var_center = model.predict(center_coord)
    print("--- Test 3: Center Point ---")
    print(f"Expected: ~3.0 | Predicted: {pred_center:.4f}")
    print(f"Variance: {var_center:.6f}\n")

    assert np.isclose(pred_center, 3.0, atol=0.2), "Center point prediction failed"
    print("All tests passed successfully.")





