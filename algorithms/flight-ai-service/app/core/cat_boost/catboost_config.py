from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class CatBoostHyperParams:
    ITERATIONS: int = 1000
    LEARNING_RATE: float = 0.05
    DEPTH: int = 6
    LOSS_FUNCTION: str = "RMSE"
    RANDOM_SEED: int = 42
    VERBOSE_STEP: int = 100
    EARLY_STOPPING_ROUNDS: int = 50


@dataclass(frozen=True)
class TrainingConfig:
    TARGET_COLUMN: str = "fuel_flow"
    GROUP_COLUMN: str = "flight_id"
    TEST_SIZE: float = 0.2
    SPLIT_RANDOM_STATE: int = 42
    MIN_PREDICTED_FUEL_COST: float = 0.0


# Enforces consistent feature ordering between training and A* pathfinding
FEATURE_COLUMNS: tuple[str, ...] = (
    "kriging_delta",
    "gross_weight",
    "delta_alt",
    "headwind",
    "crosswind",
    "ias",
    "temp",
    "alt",
)

DEFAULT_DATASET_RELATIVE_PATH: Path = Path("data") / "processed" / "nasa_training_data.parquet"
DEFAULT_MODEL_RELATIVE_PATH: Path = Path("models") / "catboost_model.cbm"