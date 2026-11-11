from catboost import CatBoostRegressor
import os

class VortexCatBoostModel:
    def __init__(self):

        self.model = CatBoostRegressor(
            iterations=1000,       # מקסימום עצי החלטה שהאלגוריתם יבנה
            learning_rate=0.05,    # קצב למידה מתון למניעת קפיצות חדות
            depth=6,               # עומק עץ סטנדרטי שמונע Overfitting
            loss_function='RMSE',  # פונקציית השגיאה שאנחנו מנסים למזער
            verbose=100            # מדפיס למסך התקדמות כל 100 איטרציות
        )

    def train_model(self, train_features, train_fuel_cost, test_features, test_fuel_cost):
        print("stat training the model")
        self.model.fit(
            train_features, train_fuel_cost,
            eval_set=(test_features, test_fuel_cost),
            early_stopping_rounds=50
        )


    def predict_cost(self, features):
        return self.model.predict(features)

    def save_to_file(self, file_path='models/catboost_model.cbm'):
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        self.model.save_model(file_path)
        print(f"the model saved in path: {file_path}")