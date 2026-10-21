import numpy as np

from app.core import kriging
from app.core.config import KrigingConfig, DEFAULT_KRIGING_CONFIG


class OrdinaryKriging:
    def __init__(self, config: KrigingConfig = DEFAULT_KRIGING_CONFIG):
        self.config = config

        self._coords: np.ndarray | None = None
        self._deltas: np.ndarray | None = None
        self._sill: float | None = None
        self._kriging_matrix: np.ndarray | None = None

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



