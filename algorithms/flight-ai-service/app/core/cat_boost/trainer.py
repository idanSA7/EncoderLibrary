from pathlib import Path
import pandas as pd
from sklearn.model_selection import GroupShuffleSplit

from catboost_config import (
    DEFAULT_DATASET_RELATIVE_PATH,
    DEFAULT_MODEL_RELATIVE_PATH,
    TrainingConfig,
)
from catboost_trainer import VortexCatBoostModel


def split_flights_train_test(df: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Splits dataset by whole flights to prevent temporal data leakage."""
    splitter = GroupShuffleSplit(
        n_splits=1,
        test_size=TrainingConfig.TEST_SIZE,
        random_state=TrainingConfig.SPLIT_RANDOM_STATE,
    )
    target = df[TrainingConfig.TARGET_COLUMN]
    groups = df[TrainingConfig.GROUP_COLUMN]
    train_idx, test_idx = next(splitter.split(df, target, groups=groups))
    return df.iloc[train_idx], df.iloc[test_idx]


def run_training_pipeline() -> None:
    algorithms_root = Path(__file__).resolve().parents[4]
    data_path = algorithms_root / DEFAULT_DATASET_RELATIVE_PATH
    model_output_path = algorithms_root / DEFAULT_MODEL_RELATIVE_PATH

    print(f"Loading dataset from: {data_path}")
    df = pd.read_parquet(data_path)

    train_df, test_df = split_flights_train_test(df)
    group_col = TrainingConfig.GROUP_COLUMN
    target_col = TrainingConfig.TARGET_COLUMN

    print(f"Train rows: {len(train_df):,} ({train_df[group_col].nunique()} flights)")
    print(f"Test rows:  {len(test_df):,} ({test_df[group_col].nunique()} flights)")

    vortex_model = VortexCatBoostModel()
    vortex_model.train_model(
        train_features=train_df,
        train_fuel_cost=train_df[target_col],
        test_features=test_df,
        test_fuel_cost=test_df[target_col],
    )

    vortex_model.evaluate_model(test_df, test_df[target_col])
    vortex_model.save_to_file(model_output_path)


if __name__ == "__main__":
    run_training_pipeline()