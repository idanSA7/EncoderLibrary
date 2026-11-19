from pathlib import Path
import pandas as pd
from sklearn.model_selection import GroupShuffleSplit
from catboost_trainer import VortexCatBoostModel


def run_training_pipeline():
    # עלייה 5 רמות למעלה מהקובץ הנוכחי אל שורש תיקיית algorithms
    algorithms_root = Path(__file__).resolve().parents[4]
    data_path = algorithms_root / "data" / "processed" / "nasa_training_data.parquet"
    model_output_path = algorithms_root / "models" / "catboost_model.cbm"

    print(f"Loading dataset from: {data_path}")
    df = pd.read_parquet(data_path)

    # חלוקת Train / Test ברמת הטיסה השלמה למניעת זליגת מידע
    splitter = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=42)
    train_idx, test_idx = next(splitter.split(df, df['fuel_flow'], groups=df['flight_id']))

    train_df = df.iloc[train_idx]
    test_df = df.iloc[test_idx]

    print(f"Train rows: {len(train_df):,} ({train_df['flight_id'].nunique()} flights)")
    print(f"Test rows:  {len(test_df):,} ({test_df['flight_id'].nunique()} flights)")

    vortex_model = VortexCatBoostModel()
    vortex_model.train_model(
        train_features=train_df,
        train_fuel_cost=train_df['fuel_flow'],
        test_features=test_df,
        test_fuel_cost=test_df['fuel_flow']
    )

    vortex_model.evaluate_model(test_df, test_df['fuel_flow'])
    vortex_model.save_to_file(str(model_output_path))


if __name__ == "__main__":
    run_training_pipeline()