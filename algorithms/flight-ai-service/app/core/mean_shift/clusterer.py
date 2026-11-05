import numpy as np
from sklearn.cluster import MeanShift
from sklearn.preprocessing import StandardScaler
from app.core.mean_shift.config import MeanShiftConfig, DEFAULT_MEAN_SHIFT_CONFIG

class FlightMeanShiftClusterer:
    def __init__(self, config: MeanShiftConfig = DEFAULT_MEAN_SHIFT_CONFIG):
        self.config = config
        self.scaler = StandardScaler()  # <-- השורה שהייתה חסרה
        self.model = MeanShift(bandwidth=self.config.bandwidth, bin_seeding=True)

    def fit_predict(self, data: np.ndarray) -> np.ndarray:
        if data.size == 0:
            return np.array([])

        scaled_data = self.scaler.fit_transform(data)
        self.model.fit(scaled_data)
        scaled_centers = self.model.cluster_centers_

        return self.scaler.inverse_transform(scaled_centers)