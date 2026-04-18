from dataclasses import dataclass, field
from typing import Dict, List, Any, Optional

@dataclass
class ExecutionEvent:
    step: int
    event_type: str
    details: Dict[str, Any]

@dataclass
class ExecutionTrace:
    events: List[ExecutionEvent] = field(default_factory=list)

@dataclass
class ExecutionResult:
    final_state: Any  # SceneState
    trace: ExecutionTrace
    success: bool = True
    error: Optional[str] = None