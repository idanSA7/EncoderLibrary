from pathlib import Path
import numpy as np
import pandas as pd
from catboost import CatBoostRegressor
from sklearn.metrics import mean_absolute_error, r2_score, root_mean_squared_error

from catboost_config import (
    CatBoostHyperParams,
    DEFAULT_MODEL_RELATIVE_PATH,
    FEATURE_COLUMNS,
    TrainingConfig,
)


class VortexCatBoostModel:
    def __init__(self, params: CatBoostHyperParams = CatBoostHyperParams()) -> None:
        self.params = params
        self.feature_columns = list(FEATURE_COLUMNS)
        self.model = CatBoostRegressor(
            # Maximum number of sequential decision trees to build (each tree corrects previous errors)
            iterations=self.params.ITERATIONS,
            # Shrinkage factor (step size) applied to each tree's contribution to prevent overshooting
            learning_rate=self.params.LEARNING_RATE,
            # Maximum depth of each symmetric tree (2^6 = 64 leaves); balances feature interactions vs. overfitting
            depth=self.params.DEPTH,
            # Optimization objective: minimizes Root Mean Squared Error, heavily penalizing large errors
            loss_function=self.params.LOSS_FUNCTION,
            # Fixes the initial state of the pseudo-random number generator (PRNG) so data subsampling
            # and tree splits are deterministic and reproducible across runs
            random_seed=self.params.RANDOM_SEED,
            # Logs training and validation loss metrics every N iterations
            verbose=self.params.VERBOSE_STEP,
        )

    def train_model(
        self,
        train_features: pd.DataFrame,
        train_fuel_cost: pd.Series | np.ndarray,
        test_features: pd.DataFrame,
        test_fuel_cost: pd.Series | np.ndarray,
    ) -> None:
        print("Starting model training...")
        self.model.fit(
            train_features[self.feature_columns],
            train_fuel_cost,
            eval_set=(test_features[self.feature_columns], test_fuel_cost),
            early_stopping_rounds=self.params.EARLY_STOPPING_ROUNDS,
        )
        print("Model training completed.")

    @staticmethod
    def _calculate_metrics(
        y_true: pd.Series | np.ndarray, y_pred: np.ndarray
    ) -> dict[str, float]:
        return {
            # Mean Absolute Error: (1/n) * Σ|y_true - y_pred|
            # Measures average error magnitude in original units (kg/s), treating all errors linearly
            "MAE": float(mean_absolute_error(y_true, y_pred)),

            # Root Mean Squared Error: sqrt((1/n) * Σ(y_true - y_pred)^2)
            # Squares errors before averaging, penalizing large spikes heavily; critical for A* edge stability
            "RMSE": float(root_mean_squared_error(y_true, y_pred)),

            # Coefficient of Determination: 1 - (Σ(y_true - y_pred)^2 / Σ(y_true - y_mean)^2)
            # Represents the proportion of fuel flow variance explained by the features vs. a constant mean baseline
            "R2": float(r2_score(y_true, y_pred)),
        }

    def _get_feature_importance(self) -> pd.Series:
        return pd.Series(
            self.model.get_feature_importance(),
            index=self.feature_columns,
        ).sort_values(ascending=False)

    @staticmethod
    def _print_evaluation_report(metrics: dict[str, float], importance: pd.Series) -> None:
        print("\n--- Model Evaluation ---")
        print(f"MAE (Mean Absolute Error): {metrics['MAE']:.4f}")
        print(f"RMSE (Root Mean Squared Error): {metrics['RMSE']:.4f}")
        print(f"R2 Score: {metrics['R2']:.4f}")
        print("\n--- Feature Importance (%) ---")
        print(importance.round(2))

    def evaluate_model(
        self, test_features: pd.DataFrame, test_fuel_cost: pd.Series | np.ndarray
    ) -> dict[str, float]:
        """Evaluates model accuracy and prints feature importance metrics."""
        predictions = self.predict_cost(test_features)
        metrics = self._calculate_metrics(test_fuel_cost, predictions)
        importance = self._get_feature_importance()
        self._print_evaluation_report(metrics, importance)
        return metrics

    def predict_cost(self, features: pd.DataFrame | np.ndarray) -> np.ndarray:
        """Predicts fuel cost (kg/s) for DataFrames or numpy arrays passed from A*."""
        if isinstance(features, pd.DataFrame):
            features = features[self.feature_columns]

        predictions = self.model.predict(features)
        # Clamp negative values to 0.0 to maintain non-negative edge weights in A*
        return np.maximum(TrainingConfig.MIN_PREDICTED_FUEL_COST, predictions)

    def save_to_file(self, file_path: Path | str = DEFAULT_MODEL_RELATIVE_PATH) -> None:
        output_path = Path(file_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        self.model.save_model(str(output_path))
        print(f"Model saved successfully to: {output_path}")

    def load_from_file(self, file_path: Path | str = DEFAULT_MODEL_RELATIVE_PATH) -> None:
        model_path = Path(file_path)
        if not model_path.exists():
            raise FileNotFoundError(f"Model file not found at: {model_path}")
        self.model.load_model(str(model_path))
        print(f"Model loaded successfully from: {model_path}")