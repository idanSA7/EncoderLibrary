import io
import zipfile
from pathlib import Path
import numpy as np
import pandas as pd
from scipy.io import loadmat


class NasaDataProcessor:
    REQUIRED_SENSORS = [
        'ALT', 'CAS', 'SAT', 'WS', 'WD', 'TH',
        'FF_1', 'FF_2', 'FF_3', 'FF_4',
        'FQTY_1', 'FQTY_2', 'FQTY_3', 'FQTY_4',
        'LATP', 'LONP', 'PH'
    ]

    # Unit conversion constants
    FT_TO_M = 0.3048
    KTS_TO_MS = 0.514444
    LBS_TO_KG = 0.453592
    BASE_AIRCRAFT_WEIGHT_KG = 27000.0  # Typical OEW + payload for BAe 146 / Avro RJ

    def __init__(self, raw_data_dir: str | None = None, output_dir: str | None = None):
        project_root = Path(__file__).resolve().parent.parent
        self.raw_data_dir = Path(raw_data_dir) if raw_data_dir else project_root / "data" / "raw"
        self.output_dir = Path(output_dir) if output_dir else project_root / "data" / "processed"
        self.output_dir.mkdir(parents=True, exist_ok=True)

    def _resample_to_1hz(self, data: np.ndarray, rate: float, is_discrete: bool = False) -> np.ndarray:
        """Aligns a sensor array of any sampling rate to 1Hz."""
        flat = data.flatten().astype(np.float64)
        if rate == 1.0:
            return flat
        elif rate > 1.0:
            step = int(round(rate))
            usable_len = (len(flat) // step) * step
            trimmed = flat[:usable_len]
            if is_discrete:
                return trimmed[::step]
            return trimmed.reshape(-1, step).mean(axis=1)
        elif 0 < rate < 1.0:
            repeat_factor = int(round(1.0 / rate))
            return np.repeat(flat, repeat_factor)
        return flat

    def extract_single_flight(self, mat_data: dict, flight_id: str) -> pd.DataFrame | None:
        """Extracts required sensors from a .mat dictionary and aligns them into a 1Hz DataFrame."""
        resampled_dict = {}
        for sensor in self.REQUIRED_SENSORS:
            if sensor not in mat_data:
                return None

            sensor_struct = mat_data[sensor][0][0]
            raw_values = sensor_struct['data']
            rate = float(sensor_struct['Rate'][0][0])

            is_discrete = (sensor == 'PH')
            resampled_dict[sensor] = self._resample_to_1hz(raw_values, rate, is_discrete=is_discrete)

        min_len = min(len(arr) for arr in resampled_dict.values())
        if min_len == 0:
            return None

        aligned_data = {k: v[:min_len] for k, v in resampled_dict.items()}
        df = pd.DataFrame(aligned_data)
        df['flight_id'] = flight_id
        return df

    def engineer_features(self, df: pd.DataFrame) -> pd.DataFrame:
        """Filters ground phases and transforms raw sensors into the 8 model features + target."""
        # 1. Keep only airborne flight phases (3=Takeoff, 4=Climb, 5=Cruise, 6=Approach) & valid airspeed
        df = df[df['PH'].isin([3.0, 4.0, 5.0, 6.0]) & (df['CAS'] > 30.0)].copy()
        if df.empty:
            return pd.DataFrame()

        # 2. Metric conversions
        alt = df['ALT'] * self.FT_TO_M
        ias = df['CAS'] * self.KTS_TO_MS
        temp = df['SAT']

        # 3. Vertical speed / altitude delta (m/s)
        delta_alt = alt.diff().fillna(0.0)

        # 4. Wind vector decomposition relative to True Heading (TH)
        ws_ms = df['WS'] * self.KTS_TO_MS
        rel_wind_rad = np.radians(df['WD'] - df['TH'])
        headwind = ws_ms * np.cos(rel_wind_rad)
        crosswind = ws_ms * np.sin(rel_wind_rad)

        # 5. Total Gross Weight (kg)
        total_fuel_kg = (df['FQTY_1'] + df['FQTY_2'] + df['FQTY_3'] + df['FQTY_4']) * self.LBS_TO_KG
        gross_weight = self.BASE_AIRCRAFT_WEIGHT_KG + total_fuel_kg

        # 6. Kriging spatial uncertainty delta (0.0 for ground-truth sensor training)
        kriging_delta = 0.0

        # 7. Target variable: total fuel flow across all 4 engines (kg/s)
        total_ff_lbs_hr = df['FF_1'] + df['FF_2'] + df['FF_3'] + df['FF_4']
        fuel_flow = (total_ff_lbs_hr * self.LBS_TO_KG) / 3600.0

        features_df = pd.DataFrame({
            'alt': alt,
            'ias': ias,
            'temp': temp,
            'headwind': headwind,
            'crosswind': crosswind,
            'gross_weight': gross_weight,
            'delta_alt': delta_alt,
            'kriging_delta': kriging_delta,
            'fuel_flow': fuel_flow,
            'flight_id': df['flight_id']
        })

        # Clean NaNs and non-physical fuel flow readings
        features_df = features_df.dropna()
        features_df = features_df[features_df['fuel_flow'] > 0.01]
        return features_df

    def process_all_flights(self, output_filename: str = "nasa_training_data.parquet"):
        """Processes all ZIP archives in raw_data_dir and saves a single Parquet dataset."""
        zip_files = sorted(self.raw_data_dir.glob("*.zip"))
        if not zip_files:
            print(f"No .zip files found in {self.raw_data_dir}")
            return

        all_flights = []
        skipped_count = 0

        for zip_path in zip_files:
            print(f"Processing archive: {zip_path.name}...")
            with zipfile.ZipFile(zip_path, 'r') as zf:
                mat_files = [f for f in zf.namelist() if f.endswith('.mat')]
                for mat_name in mat_files:
                    flight_id = Path(mat_name).stem
                    try:
                        mat_data = loadmat(io.BytesIO(zf.read(mat_name)))
                        raw_df = self.extract_single_flight(mat_data, flight_id)
                        if raw_df is None:
                            skipped_count += 1
                            continue

                        clean_df = self.engineer_features(raw_df)
                        if not clean_df.empty:
                            all_flights.append(clean_df)
                        else:
                            skipped_count += 1
                    except Exception as e:
                        print(f"Error processing {flight_id}: {e}")
                        skipped_count += 1

        if not all_flights:
            print("No valid flight data extracted.")
            return

        final_df = pd.concat(all_flights, ignore_index=True)
        output_path = self.output_dir / output_filename
        final_df.to_parquet(output_path, index=False)

        print("\nETL Complete!")
        print(f"Valid flights processed: {len(all_flights)} | Skipped: {skipped_count}")
        print(f"Total training rows (seconds): {len(final_df):,}")
        print(f"Saved to: {output_path}")
        print("\nPreview:")
        print(final_df.head())


if __name__ == "__main__":
    processor = NasaDataProcessor()
    processor.process_all_flights()