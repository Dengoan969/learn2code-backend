from dataclasses import dataclass

@dataclass
class TaskConfig:
    # ЗАМЕНА: grid_width, grid_height → scene_width, scene_height (float)
    scene_width: float = 1000.0    # Пиксели
    scene_height: float = 1000.0   # Пиксели
    # УДАЛЕНО: tolerance_px, min_trace_ratio, level - не используются в Python