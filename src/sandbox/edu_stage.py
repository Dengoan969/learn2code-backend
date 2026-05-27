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
        self.scene_width = config.scene_width
        self.scene_height = config.scene_height
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

def _check_cat():
    global _stage
    if _stage is None:
        raise RuntimeError("Stage not initialized")
    if _stage.get_cat() is None:
        raise RuntimeError("Cat sprite not found in initial state")

def _check_steps():
    global _stage
    if _stage is None:
        raise RuntimeError("Stage not initialized")
    if _stage.tracer.step_count >= MAX_EXECUTION_STEPS:
        raise RuntimeError("Обнаружен бесконечный цикл или слишком длинная программа.")

def _check_bounding_box_collision(cat_x: float, cat_y: float, cat_width: float, cat_height: float,
                                 target_x: float, target_y: float, target_width: float, target_height: float) -> bool:
    """Check if two sprites' bounding boxes intersect."""
    cat_left = cat_x - cat_width / 2
    cat_right = cat_x + cat_width / 2
    cat_top = cat_y - cat_height / 2
    cat_bottom = cat_y + cat_height / 2
    
    target_left = target_x - target_width / 2
    target_right = target_x + target_width / 2
    target_top = target_y - target_height / 2
    target_bottom = target_y + target_height / 2
    
    # Check for intersection
    return (cat_left < target_right and cat_right > target_left and
            cat_top < target_bottom and cat_bottom > target_top)

def _calculate_movement_vector(cat: CatState, distance: float) -> tuple[float, float]:
    """Calculate movement vector based on cat's direction."""
    rad = math.radians(cat.direction)
    dx = distance * math.sin(rad)
    dy = -distance * math.cos(rad)  # Negative because up is negative Y in Scratch
    # Round near-zero values to zero to avoid floating-point errors
    epsilon = 1e-10
    if abs(dx) < epsilon:
        dx = 0.0
    if abs(dy) < epsilon:
        dy = 0.0
    return dx, dy

def _check_scene_boundaries(x: float, y: float, scene_width: float, scene_height: float) -> bool:
    """Check if position is within scene boundaries with epsilon tolerance.
    Scene is centered at (0,0), so boundaries are [-scene_width/2, scene_width/2] and similarly for Y.
    """
    epsilon = 1e-9
    half_w = scene_width / 2
    half_h = scene_height / 2
    return (-half_w - epsilon <= x <= half_w + epsilon) and (-half_h - epsilon <= y <= half_h + epsilon)

def _check_collision(cat_x: float, cat_y: float, cat_width: float, cat_height: float,
                    sprite: SpriteState) -> bool:
    """Check if cat at given position collides with sprite."""
    return _check_bounding_box_collision(
        cat_x, cat_y, cat_width, cat_height,
        sprite.x, sprite.y, sprite.width, sprite.height
    )

def _move_step_by_step(cat: CatState, dx: float, dy: float, steps: int) -> None:
    """Move cat step by step with collision detection."""
    step_dx = dx / steps
    step_dy = dy / steps
    
    for step in range(steps):
        # Calculate new position
        new_x = cat.x + step_dx
        new_y = cat.y + step_dy
        
        # Check scene boundaries
        if not _check_scene_boundaries(new_x, new_y, _stage.scene_width, _stage.scene_height):
            break  # Stop at boundary
        
        # Check collisions
        hit_wall = False
        
        for sprite in _stage.sprites:
            if sprite == cat or not sprite.visible:
                continue
            
            if not _check_collision(new_x, new_y, cat.width, cat.height, sprite):
                continue
            
            if isinstance(sprite, AppleState):
                # Collect apple immediately
                sprite.visible = False
                cat.collected_items["apple"] = cat.collected_items.get("apple", 0) + 1
                _stage.tracer.log("collision", target="apple", collected=True)
            elif isinstance(sprite, WallState):
                hit_wall = True
                _stage.tracer.log("collision", target="wall", stopped=True)
                break  # Stop checking other sprites
        
        if hit_wall:
            # Stop at current position (don't move to new_x, new_y)
            # Throw exception to stop program execution
            raise RuntimeError("Кот столкнулся со стеной")
        
        # Move to the new position (round to nearest pixel to avoid floating-point errors)
        cat.x = round(new_x)
        cat.y = round(new_y)

def move(distance: float):
    """Move the cat forward by the specified distance (in pixels)."""
    _check_cat()
    _check_steps()
    
    cat = _stage.get_cat()
    if not cat or not cat.visible:
        return
    
    # Early return for zero distance
    if abs(distance) < 0.001:
        return
    
    # Calculate movement vector based on cat's direction
    dx, dy = _calculate_movement_vector(cat, distance)
    
    # Determine number of steps for collision detection
    # Use step size of 1 pixel for precise collision detection
    step_size = 1.0
    steps = max(1, int(abs(distance) / step_size))
    
    # Perform step-by-step movement with collision detection
    _move_step_by_step(cat, dx, dy, steps)
    
    # Log the movement
    _stage.tracer.log("move",
                     distance=round(distance, 2),
                     new_x=round(cat.x, 2),
                     new_y=round(cat.y, 2),
                     direction=round(cat.direction, 2))

def turn(degrees: float):
    """Turn the cat by the specified number of degrees.
    
    Positive degrees turn clockwise (right), negative degrees turn counter-clockwise (left).
    """
    _check_cat()
    _check_steps()
    
    cat = _stage.get_cat()
    if not cat:
        return
    
    # Early return for zero degrees
    if abs(degrees) < 0.001:
        return
    
    # Apply turn (positive = clockwise/right, negative = counter-clockwise/left)
    cat.direction += degrees
    
    # Normalize direction to 0-360 range
    cat.direction %= 360
    if cat.direction < 0:
        cat.direction += 360
    
    _stage.tracer.log("turn",
                     degrees=round(degrees, 2),
                     new_direction=round(cat.direction, 2))

def say(text: str):
    """Make the cat say the specified text."""
    _check_cat()
    _check_steps()
    
    cat = _stage.get_cat()
    if not cat:
        return
    
    # Convert to string and track how many times this text was said
    text_str = str(text)
    cat.said_texts[text_str] = cat.said_texts.get(text_str, 0) + 1
    
    _stage.tracer.log("say", text=text_str)



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
        "say": say
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
