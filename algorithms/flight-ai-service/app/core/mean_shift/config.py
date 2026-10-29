from dataclasses import dataclass


@dataclass(frozen=True)
class MeanShiftConfig:
    bandwidth: float = 1
    epsilon: float = 1e-3
    max_iterations: int = 300
    cluster_merge_threshold: float = 0.5 /bandwidth


DEFAULT_MEAN_SHIFT_CONFIG = MeanShiftConfig()