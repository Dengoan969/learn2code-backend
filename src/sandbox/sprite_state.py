from enum import Enum
from dataclasses import dataclass, field
from typing import Dict, List, Any

class SpriteType(Enum):
    CAT = "cat"
    APPLE = "apple"
    WALL = "wall"

@dataclass
class SpriteState:
    type: SpriteType
    # ЗАМЕНА: grid_x, grid_y → x, y (float)
    x: float = 0.0          # Пиксели, центр спрайта
    y: float = 0.0          # Пиксели, центр спрайта
    # НОВОЕ: Размеры спрайта
    width: float = 50.0     # Ширина в пикселях
    height: float = 50.0    # Высота в пикселях
    visible: bool = True

@dataclass
class CatState(SpriteState):
    direction: float = 90.0
    costume: str = "default"
    said_texts: Dict[str, int] = field(default_factory=dict)
    collected_items: Dict[str, int] = field(default_factory=dict)  # ОСТАВИТЬ - сбор яблок
    
    def __post_init__(self):
        self.type = SpriteType.CAT

@dataclass
class AppleState(SpriteState):
    def __post_init__(self):
        self.type = SpriteType.APPLE

@dataclass
class WallState(SpriteState):
    def __post_init__(self):
        self.type = SpriteType.WALL

@dataclass
class SceneState:
    sprites: List[SpriteState] = field(default_factory=list)