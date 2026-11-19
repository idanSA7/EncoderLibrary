from dataclasses import dataclass


@dataclass(frozen=True)
class PhysicsConstants:
    FT_TO_M: float = 0.3048
    KTS_TO_MS: float = 0.514444
    LBS_TO_KG: float = 0.453592
    SECONDS_PER_HOUR: float = 3600.0


@dataclass(frozen=True)
class AircraftConfig:
    # Typical Operating Empty Weight (OEW) + standard payload for BAe 146 / Avro RJ
    BASE_WEIGHT_KG: float = 27000.0
    ENGINE_FUEL_FLOW_SENSORS: tuple[str, ...] = ('FF_1', 'FF_2', 'FF_3', 'FF_4')
    FUEL_QUANTITY_SENSORS: tuple[str, ...] = ('FQTY_1', 'FQTY_2', 'FQTY_3', 'FQTY_4')


@dataclass(frozen=True)
class FilterThresholds:
    # Flight phases: 3=Takeoff, 4=Climb, 5=Cruise, 6=Approach
    AIRBORNE_PHASES: tuple[float, ...] = (3.0, 4.0, 5.0, 6.0)
    MIN_AIRSPEED_KTS: float = 30.0
    MIN_VALID_FUEL_FLOW_KG_S: float = 0.01
    DEFAULT_KRIGING_DELTA: float = 0.0


DISCRETE_SENSORS: frozenset[str] = frozenset({'PH'})

REQUIRED_SENSORS: tuple[str, ...] = (
    'ALT', 'CAS', 'SAT', 'WS', 'WD', 'TH',
    'FF_1', 'FF_2', 'FF_3', 'FF_4',
    'FQTY_1', 'FQTY_2', 'FQTY_3', 'FQTY_4',
    'LATP', 'LONP', 'PH'
)

DEFAULT_OUTPUT_FILENAME: str = "nasa_training_data.parquet"