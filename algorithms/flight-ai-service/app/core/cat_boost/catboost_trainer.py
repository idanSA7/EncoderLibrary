import os
import numpy as np
import pandas as pd
from catboost import CatBoostRegressor
from sklearn.metrics import mean_absolute_error, root_mean_squared_error, r2_score


class VortexCatBoostModel:
    # Enforce consistent feature ordering between training and A* pathfinding
    FEATURE_COLUMNS = [
        'kriging_delta', 'gross_weight', 'delta_alt',
        'headwind', 'crosswind', 'ias', 'temp', 'alt'
    ]
    def __init__(self):
        self.model = CatBoostRegressor(
            iterations=1000,       # Maximum number of decision trees
            learning_rate=0.05,    # Moderate learning rate to prevent sharp jumps
            depth=6,               # Standard tree depth to prevent overfitting
            loss_function='RMSE',  # Loss function to minimize
            random_seed=42,        # For reproducible results across runs
            verbose=100            # Print training progress every 100 iterations
        )

    def train_model(self, train_features, train_fuel_cost, test_features, test_fuel_cost):
        print("Starting model training...")
        self.model.fit(
            train_features[self.FEATURE_COLUMNS], train_fuel_cost,
            eval_set=(test_features[self.FEATURE_COLUMNS], test_fuel_cost),
            early_stopping_rounds=50
        )
        print("Model training completed.")

    def evaluate_model(self, test_features, test_fuel_cost) -> dict:
        #Evaluates model accuracy and prints feature importance metrics.
        preds = self.predict_cost(test_features)

        metrics = {
            "MAE": mean_absolute_error(test_fuel_cost, preds),
            "RMSE": root_mean_squared_error(test_fuel_cost, preds),
            "R2": r2_score(test_fuel_cost, preds)
        }

        print("\n--- Model Evaluation ---")
        print(f"MAE (Mean Absolute Error): {metrics['MAE']:.4f}")
        print(f"RMSE (Root Mean Squared Error): {metrics['RMSE']:.4f}")
        print(f"R2 Score: {metrics['R2']:.4f}")

        importance = pd.Series(
            self.model.get_feature_importance(),
            index=self.FEATURE_COLUMNS
        ).sort_values(ascending=False)

        print("\n--- Feature Importance (%) ---")
        print(importance.round(2))

        return metrics

    def predict_cost(self, features):
        # Supports both pandas DataFrames and numpy arrays passed from A*
        if isinstance(features, pd.DataFrame):
            features = features[self.FEATURE_COLUMNS]

        predictions = self.model.predict(features)
        # Clamp negative values to 0.0 to maintain non-negative edge weights in A*
        return np.maximum(0.0, predictions)

    def save_to_file(self, file_path='models/catboost_model.cbm'):
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        self.model.save_model(file_path)
        print(f"Model saved successfully to: {file_path}")

    def load_from_file(self, file_path='models/catboost_model.cbm'):
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"Model file not found at: {file_path}")
        self.model.load_model(file_path)
        print(f"Model loaded successfully from: {file_path}")