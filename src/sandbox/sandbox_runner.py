import sys
import json
from edu_stage import execute_student_code
from sprite_state import SceneState, SpriteState, CatState, AppleState, WallState, SpriteType
from task_config import TaskConfig
from execution_trace import ExecutionTrace

def dict_to_scene_state(data: dict) -> SceneState:
    scene = SceneState()
    if not data or "sprites" not in data:
        return scene
    
    for sprite_dict in data["sprites"]:
        sprite_type = sprite_dict.get("type")
        x = sprite_dict.get("x", 0.0)
        y = sprite_dict.get("y", 0.0)
        width = sprite_dict.get("width", 50.0)
        height = sprite_dict.get("height", 50.0)
        visible = sprite_dict.get("visible", True)
        
        if sprite_type == SpriteType.CAT.value:
            sprite = CatState(
                type=SpriteType.CAT,
                x=x,
                y=y,
                width=width,
                height=height,
                direction=sprite_dict.get("direction", 90.0),
                costume=sprite_dict.get("costume", "default"),
                said_texts=sprite_dict.get("saidTexts", {}),
                collected_items=sprite_dict.get("collectedItems", {})
            )
        elif sprite_type == SpriteType.APPLE.value:
            sprite = AppleState(
                type=SpriteType.APPLE,
                x=x,
                y=y,
                width=width,
                height=height
            )
        elif sprite_type == SpriteType.WALL.value:
            sprite = WallState(
                type=SpriteType.WALL,
                x=x,
                y=y,
                width=width,
                height=height
            )
        else:
            continue
        sprite.visible = visible
        scene.sprites.append(sprite)
    
    return scene

def dict_to_task_config(data: dict) -> TaskConfig:
    if not data:
        return TaskConfig()
    
    return TaskConfig(
        scene_width=data.get("sceneWidth", 1000.0),
        scene_height=data.get("sceneHeight", 1000.0)
    )

def scene_state_to_dict(scene: SceneState) -> dict:
    sprites = []
    for sprite in scene.sprites:
        sprite_dict = {
            "type": sprite.type.value,
            "x": sprite.x,
            "y": sprite.y,
            "width": sprite.width,
            "height": sprite.height,
            "visible": sprite.visible
        }
        if isinstance(sprite, CatState):
            sprite_dict["direction"] = sprite.direction
            sprite_dict["costume"] = sprite.costume
            sprite_dict["saidTexts"] = sprite.said_texts
            sprite_dict["collectedItems"] = sprite.collected_items
        sprites.append(sprite_dict)
    
    return {"sprites": sprites}

def execution_trace_to_dict(trace) -> dict:
    events = []
    for event in trace.events:
        events.append({
            "step": event.step,
            "eventType": event.event_type,
            "details": event.details
        })
    return {"events": events}

def main():
    input_data = sys.stdin.read()
    try:
        request = json.loads(input_data)
    except json.JSONDecodeError as e:
        print(json.dumps({
            "success": False,
            "error": f"Invalid JSON input: {str(e)}",
            "finalState": scene_state_to_dict(SceneState()),
            "trace": execution_trace_to_dict(ExecutionTrace())
        }))
        sys.exit(1)

    code = request.get("code", "")
    initialState = request.get("initialState", {})
    config = request.get("config", {})

    if not code.strip():
        print(json.dumps({
            "success": False,
            "error": "Код пустой",
            "finalState": scene_state_to_dict(SceneState()),
            "trace": execution_trace_to_dict(ExecutionTrace())
        }))
        sys.exit(1)

    scene_state = dict_to_scene_state(initialState)
    task_config = dict_to_task_config(config)

    result = execute_student_code(code, scene_state, task_config)
    
    output = {
        "success": result.success,
        "error": result.error,
        "finalState": scene_state_to_dict(result.final_state),
        "trace": execution_trace_to_dict(result.trace)
    }
    
    print(json.dumps(output))

if __name__ == "__main__":
    main()
