from dataclasses import dataclass


@dataclass(frozen=True)
class KrigingConfig:
    default_range_a: float = 40.0
    default_sill_c: float = 0.5
    min_variance_floor: float = 1e-4


DEFAULT_KRIGING_CONFIG = KrigingConfig()