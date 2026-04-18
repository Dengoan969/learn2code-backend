from enum import Enum
from dataclasses import dataclass, field
from typing import Dict, List, Any

class SpriteType(Enum):
    CAT = "Cat"
    APPLE = "Apple"
    WALL = "Wall"

@dataclass
class SpriteState:
    type: SpriteType
    grid_x: int = 0
    grid_y: int = 0
    visible: bool = True

@dataclass
class CatState(SpriteState):
    direction: float = 90.0
    costume: str = "default"
    said_texts: Dict[str, int] = field(default_factory=dict)
    collected_items: Dict[str, int] = field(default_factory=dict)
    
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