from dataclasses import dataclass
from enum import Enum

class CheckLevel(Enum):
    Normal = "Normal"
    Strict = "Strict"
    Lenient = "Lenient"

@dataclass
class TaskConfig:
    grid_width: int = 20
    grid_height: int = 20
    tolerance_px: float = 5.0
    min_trace_ratio: float = 0.7
    level: CheckLevel = CheckLevel.Normal