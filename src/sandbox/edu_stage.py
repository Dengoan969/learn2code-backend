# edu_stage.py
import json
import math
from dataclasses import asdict
from typing import Dict, List, Optional, Any
from config import MAX_EXECUTION_STEPS, COLLISION_THRESHOLD
from sprite_state import SpriteType, SpriteState, CatState, AppleState, WallState, SceneState
from task_config import TaskConfig
from execution_trace import ExecutionEvent, ExecutionTrace, ExecutionResult

class ExecutionTracer:
    def __init__(self):
        self.events: List[ExecutionEvent] = []
        self.step_count: int = 0

    def log(self, event_type: str, **kwargs):
        self.step_count += 1
        if self.step_count > MAX_EXECUTION_STEPS:
            raise RuntimeError(f"Превышен лимит выполнения ({MAX_EXECUTION_STEPS} шагов). Проверьте циклы.")
        self.events.append(ExecutionEvent(step=self.step_count, event_type=event_type, details=kwargs))

class Stage:
    def __init__(self, config: TaskConfig):
        self.grid_width = config.grid_width
        self.grid_height = config.grid_height
        self.sprites: List[SpriteState] = []
        self.tracer = ExecutionTracer()

    def add_sprite(self, sprite: SpriteState):
        self.sprites.append(sprite)

    def get_cat(self) -> Optional[CatState]:
        for sprite in self.sprites:
            if isinstance(sprite, CatState):
                return sprite
        return None

    def get_state(self) -> SceneState:
        return SceneState(sprites=self.sprites.copy())

    def get_trace(self) -> ExecutionTrace:
        return ExecutionTrace(events=self.tracer.events.copy())

    def reset(self):
        self.sprites.clear()
        self.tracer = ExecutionTracer()

_stage: Optional[Stage] = None

def _ensure_cat():
    global _stage
    if _stage is None:
        raise RuntimeError("Stage not initialized")
    if _stage.get_cat() is None:
        _stage.add_sprite(CatState(type=SpriteType.CAT))

def _check_steps():
    global _stage
    if _stage is None:
        raise RuntimeError("Stage not initialized")
    if _stage.tracer.step_count >= MAX_EXECUTION_STEPS:
        raise RuntimeError("Обнаружен бесконечный цикл или слишком длинная программа.")

def move(cells: int):
    _ensure_cat()
    _check_steps()
    cat = _stage.get_cat()
    if not cat or not cat.visible:
        return
    
    # Convert direction to radians (Scratch: 0=up, 90=right, 180=down, 270=left)
    # Note: In Scratch, up is negative Y, down is positive Y
    rad = math.radians(cat.direction)
    dx = cells * math.sin(rad)
    dy = -cells * math.cos(rad)  # Negative because up is negative Y in Scratch
    
    # Move in grid cells (round to nearest integer)
    new_x = cat.grid_x + round(dx)
    new_y = cat.grid_y + round(dy)
    
    # Clamp to grid bounds
    new_x = max(0, min(new_x, _stage.grid_width - 1))
    new_y = max(0, min(new_y, _stage.grid_height - 1))
    
    cat.grid_x = new_x
    cat.grid_y = new_y
    _stage.tracer.log("move", cells=cells, new_x=new_x, new_y=new_y)

def turn(degrees: float, direction: str = "right"):
    _ensure_cat()
    _check_steps()
    cat = _stage.get_cat()
    if not cat:
        return
    cat.direction += degrees if direction == "right" else -degrees
    cat.direction %= 360
    _stage.tracer.log("turn", degrees=degrees, direction=direction, new_direction=round(cat.direction, 2))

def say(text: str):
    _ensure_cat()
    _check_steps()
    cat = _stage.get_cat()
    if not cat:
        return
    # Increment count for this text
    cat.said_texts[text] = cat.said_texts.get(text, 0) + 1
    _stage.tracer.log("say", text=str(text))

def check_collision(target_type: str) -> bool:
    _ensure_cat()
    _check_steps()
    cat = _stage.get_cat()
    if not cat:
        return False
    
    target_sprites = [s for s in _stage.sprites if s.type.value == target_type]
    if not target_sprites:
        return False
    
    for target in target_sprites:
        dist = math.hypot(cat.grid_x - target.grid_x, cat.grid_y - target.grid_y)
        touching = dist <= COLLISION_THRESHOLD
        if touching:
            # If target is apple, collect it
            if isinstance(target, AppleState):
                target.visible = False
                cat.collected_items["apple"] = cat.collected_items.get("apple", 0) + 1
            _stage.tracer.log("collision", target=target_type, touching=True, distance=round(dist, 2))
            return True
    
    _stage.tracer.log("collision", target=target_type, touching=False, distance=0)
    return False

def show_sprite(sprite_type: str):
    _check_steps()
    for sprite in _stage.sprites:
        if sprite.type.value == sprite_type:
            sprite.visible = True
            _stage.tracer.log("show", type=sprite_type)
            break

def hide_sprite(sprite_type: str):
    _check_steps()
    for sprite in _stage.sprites:
        if sprite.type.value == sprite_type:
            sprite.visible = False
            _stage.tracer.log("hide", type=sprite_type)
            break

def execute_student_code(code: str, initialState: SceneState, config: TaskConfig) -> ExecutionResult:
    global _stage
    _stage = Stage(config)
    
    # Add initial sprites
    for sprite in initialState.sprites:
        _stage.add_sprite(sprite)
    
    safe_globals = {
        "__builtins__": __builtins__,
        "__name__": "__main__",
        "move": move,
        "turn": turn,
        "say": say,
        "check_collision": check_collision,
        "show_sprite": show_sprite,
        "hide_sprite": hide_sprite
    }

    try:
        exec(code, safe_globals)
        return ExecutionResult(
            final_state=_stage.get_state(),
            trace=_stage.get_trace(),
            success=True,
            error=None
        )
    except Exception as e:
        return ExecutionResult(
            final_state=_stage.get_state() if _stage else SceneState(),
            trace=_stage.get_trace() if _stage else ExecutionTrace(),
            success=False,
            error=str(e)
        )
