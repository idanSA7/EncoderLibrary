import io
import zipfile
from pathlib import Path
import numpy as np
import pandas as pd
from scipy.io import loadmat

from etl_config import (
    AircraftConfig,
    DEFAULT_OUTPUT_FILENAME,
    DISCRETE_SENSORS,
    FilterThresholds,
    PhysicsConstants,
    REQUIRED_SENSORS,
)


class NasaDataProcessor:
    def __init__(self, raw_data_dir: str | None = None, output_dir: str | None = None):
        project_root = Path(__file__).resolve().parent.parent
        self.raw_data_dir = Path(raw_data_dir) if raw_data_dir else project_root / "data" / "raw"
        self.output_dir = Path(output_dir) if output_dir else project_root / "data" / "processed"
        self.output_dir.mkdir(parents=True, exist_ok=True)

    @staticmethod
    def _resample_to_1hz(data: np.ndarray, rate: float, is_discrete: bool = False) -> np.ndarray:
        """Aligns a sensor array of any sampling rate to 1Hz."""
        flat = data.flatten().astype(np.float64)
        if rate == 1.0:
            return flat
        if rate > 1.0:
            step = int(round(rate))
            usable_len = (len(flat) // step) * step
            trimmed = flat[:usable_len]
            return trimmed[::step] if is_discrete else trimmed.reshape(-1, step).mean(axis=1)
        if 0 < rate < 1.0:
            repeat_factor = int(round(1.0 / rate))
            return np.repeat(flat, repeat_factor)
        return flat

    def extract_single_flight(self, mat_data: dict, flight_id: str) -> pd.DataFrame | None:
        """Extracts required sensors from a .mat dictionary and aligns them into a 1Hz DataFrame."""
        resampled_sensors: dict[str, np.ndarray] = {}

        for sensor in REQUIRED_SENSORS:
            if sensor not in mat_data:
                return None

            sensor_struct = mat_data[sensor][0][0]
            raw_values = sensor_struct['data']
            rate = float(sensor_struct['Rate'][0][0])

            resampled_sensors[sensor] = self._resample_to_1hz(
                raw_values, rate, is_discrete=(sensor in DISCRETE_SENSORS)
            )

        min_len = min(len(arr) for arr in resampled_sensors.values())
        if min_len == 0:
            return None

        aligned_data = {sensor: values[:min_len] for sensor, values in resampled_sensors.items()}
        df = pd.DataFrame(aligned_data)
        df['flight_id'] = flight_id
        return df

    @staticmethod
    def _filter_airborne_phases(df: pd.DataFrame) -> pd.DataFrame:
        is_airborne = df['PH'].isin(FilterThresholds.AIRBORNE_PHASES)
        is_valid_speed = df['CAS'] > FilterThresholds.MIN_AIRSPEED_KTS
        return df[is_airborne & is_valid_speed].copy()

    @staticmethod
    def _decompose_wind_vectors(df: pd.DataFrame) -> tuple[pd.Series, pd.Series]:
        wind_speed_ms = df['WS'] * PhysicsConstants.KTS_TO_MS
        relative_wind_rad = np.radians(df['WD'] - df['TH'])
        headwind = wind_speed_ms * np.cos(relative_wind_rad)
        crosswind = wind_speed_ms * np.sin(relative_wind_rad)
        return headwind, crosswind

    @staticmethod
    def _calculate_gross_weight_kg(df: pd.DataFrame) -> pd.Series:
        total_fuel_lbs = df[list(AircraftConfig.FUEL_QUANTITY_SENSORS)].sum(axis=1)
        total_fuel_kg = total_fuel_lbs * PhysicsConstants.LBS_TO_KG
        return AircraftConfig.BASE_WEIGHT_KG + total_fuel_kg

    @staticmethod
    def _calculate_fuel_flow_kg_s(df: pd.DataFrame) -> pd.Series:
        total_ff_lbs_hr = df[list(AircraftConfig.ENGINE_FUEL_FLOW_SENSORS)].sum(axis=1)
        return (total_ff_lbs_hr * PhysicsConstants.LBS_TO_KG) / PhysicsConstants.SECONDS_PER_HOUR

    def engineer_features(self, df: pd.DataFrame) -> pd.DataFrame:
        """Filters ground phases and transforms raw sensors into the 8 model features + target."""
        airborne_df = self._filter_airborne_phases(df)
        if airborne_df.empty:
            return pd.DataFrame()

        alt = airborne_df['ALT'] * PhysicsConstants.FT_TO_M
        ias = airborne_df['CAS'] * PhysicsConstants.KTS_TO_MS
        delta_alt = alt.diff().fillna(0.0)
        headwind, crosswind = self._decompose_wind_vectors(airborne_df)
        gross_weight = self._calculate_gross_weight_kg(airborne_df)
        fuel_flow = self._calculate_fuel_flow_kg_s(airborne_df)

        features_df = pd.DataFrame({
            'alt': alt,
            'ias': ias,
            'temp': airborne_df['SAT'],
            'headwind': headwind,
            'crosswind': crosswind,
            'gross_weight': gross_weight,
            'delta_alt': delta_alt,
            'kriging_delta': FilterThresholds.DEFAULT_KRIGING_DELTA,
            'fuel_flow': fuel_flow,
            'flight_id': airborne_df['flight_id'],
        })

        features_df = features_df.dropna()
        return features_df[features_df['fuel_flow'] > FilterThresholds.MIN_VALID_FUEL_FLOW_KG_S]

    def _process_mat_entry(self, archive: zipfile.ZipFile, mat_filename: str) -> pd.DataFrame | None:
        """Loads, aligns, and extracts features for a single .mat flight file inside an archive."""
        flight_id = Path(mat_filename).stem
        try:
            mat_bytes = io.BytesIO(archive.read(mat_filename))
            mat_data = loadmat(mat_bytes)
        except (OSError, ValueError, KeyError) as exc:
            print(f"Error reading {flight_id}: {exc}")
            return None

        raw_df = self.extract_single_flight(mat_data, flight_id)
        if raw_df is None:
            return None

        clean_df = self.engineer_features(raw_df)
        return None if clean_df.empty else clean_df

    def _process_archive(self, zip_path: Path) -> tuple[list[pd.DataFrame], int]:
        """Processes all .mat files within a single ZIP archive."""
        print(f"Processing archive: {zip_path.name}...")
        valid_flights: list[pd.DataFrame] = []
        skipped_count = 0

        try:
            with zipfile.ZipFile(zip_path, 'r') as archive:
                mat_files = [name for name in archive.namelist() if name.endswith('.mat')]
                for mat_name in mat_files:
                    flight_df = self._process_mat_entry(archive, mat_name)
                    if flight_df is not None:
                        valid_flights.append(flight_df)
                    else:
                        skipped_count += 1
        except (zipfile.BadZipFile, OSError) as exc:
            print(f"Failed to open archive {zip_path.name}: {exc}")

        return valid_flights, skipped_count

    def process_all_flights(self, output_filename: str = DEFAULT_OUTPUT_FILENAME) -> None:
        """Processes all ZIP archives in raw_data_dir and saves a single Parquet dataset."""
        zip_files = sorted(self.raw_data_dir.glob("*.zip"))
        if not zip_files:
            print(f"No .zip files found in {self.raw_data_dir}")
            return

        all_flights: list[pd.DataFrame] = []
        total_skipped = 0

        for zip_path in zip_files:
            flights, skipped = self._process_archive(zip_path)
            all_flights.extend(flights)
            total_skipped += skipped

        if not all_flights:
            print("No valid flight data extracted.")
            return

        final_df = pd.concat(all_flights, ignore_index=True)
        output_path = self.output_dir / output_filename
        final_df.to_parquet(output_path, index=False)

        self._print_summary(len(all_flights), total_skipped, final_df, output_path)

    @staticmethod
    def _print_summary(valid_count: int, skipped_count: int, df: pd.DataFrame, output_path: Path) -> None:
        print("\nETL Complete!")
        print(f"Valid flights processed: {valid_count} | Skipped: {skipped_count}")
        print(f"Total training rows (seconds): {len(df):,}")
        print(f"Saved to: {output_path}")
        print("\nPreview:")
        print(df.head())


if __name__ == "__main__":
    processor = NasaDataProcessor()
    processor.process_all_flights()