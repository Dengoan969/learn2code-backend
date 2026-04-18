# ast_extractor.py
import ast
import re
import json
from typing import Dict, List, Optional

SEMANTIC_MAP = {
    "move": "move",
    "step": "move",
    "turn": "turn",
    "rotate": "turn",
    "say": "say",
    "speak": "say",
    "check_collision": "condition",
    "show_sprite": "visibility",
    "hide_sprite": "visibility",
    "if": "condition",
    "controls_if": "condition",
    "for": "loop",
    "while": "loop",
    "controls_repeat_ext": "loop"
}

TYPE_MAP = {
    ast.If: "Condition",
    ast.IfExp: "Condition",
    ast.For: "Loop",
    ast.While: "Loop",
    ast.Call: "FunctionCall",
    ast.Assign: "Assignment"
}


def extract_block_ids(code: str) -> Dict[int, dict]:
    block_map = {}
    current = None
    for i, line in enumerate(code.splitlines(), 1):
        m = re.search(r"#\s*BLOCK_ID:\s*([^|\s]+)(?:\|TYPE:\s*([^\s]+))?", line)
        if m:
            block_id = m.group(1)
            type_val = m.group(2).strip() if m.group(2) else None
            current = {"blockId": block_id, "type": type_val}
        if current:
            block_map[i] = current
    return block_map


def get_type(node):
    for cls, t in TYPE_MAP.items():
        if isinstance(node, cls):
            return t
    return "Unknown"


def get_semantic(node):
    if isinstance(node, ast.Call):
        name = getattr(node.func, 'id', None)
        return SEMANTIC_MAP.get(name, name)
    if isinstance(node, (ast.If, ast.IfExp)):
        return "condition"
    if isinstance(node, (ast.For, ast.While)):
        return "loop"
    return None


def extract_parameters(node):
    """Extract parameters from AST node."""
    params = {}
    if isinstance(node, ast.Call):
        # Extract positional arguments
        for i, arg in enumerate(node.args):
            if isinstance(arg, ast.Constant):
                params[f"arg{i}"] = arg.value
            elif isinstance(arg, ast.Name):
                params[f"arg{i}"] = arg.id
            elif isinstance(arg, ast.Num):  # Python 3.7 compatibility
                params[f"arg{i}"] = arg.n
            elif isinstance(arg, ast.Str):
                params[f"arg{i}"] = arg.s
            else:
                params[f"arg{i}"] = str(arg)
        # Extract keyword arguments
        for kw in node.keywords:
            key = kw.arg or "unnamed"
            if isinstance(kw.value, ast.Constant):
                params[key] = kw.value.value
            elif isinstance(kw.value, ast.Name):
                params[key] = kw.value.id
            elif isinstance(kw.value, ast.Num):
                params[key] = kw.value.n
            elif isinstance(kw.value, ast.Str):
                params[key] = kw.value.s
            else:
                params[key] = str(kw.value)
    elif isinstance(node, (ast.If, ast.IfExp)):
        params["condition"] = "expression"
    elif isinstance(node, (ast.For, ast.While)):
        params["iterable"] = "expression"
    return params


def extract_normalized_ast(code: str) -> dict:
    try:
        tree = ast.parse(code)
    except SyntaxError as e:
        return {"success": False, "error": str(e)}

    block_map = extract_block_ids(code)
    elements = []
    metrics = {
        "loopCount": 0,
        "conditionCount": 0,
        "functionCalls": 0,
        "complexity": 0
    }

    for node in ast.walk(tree):
        if isinstance(node, (ast.Module, ast.Expr, ast.Load, ast.Constant, ast.Name, ast.Attribute)):
            continue

        line = getattr(node, 'lineno', 0)
        b_info = block_map.get(line, {})
        elem_type = get_type(node)

        elem = {
            "type": elem_type,
            "semanticHint": get_semantic(node),
            "line": line,
            "blockId": b_info.get("blockId"),
            "parameters": extract_parameters(node)
        }
        elements.append(elem)
        metrics["complexity"] += 1
        if elem_type == "Loop":
            metrics["loopCount"] += 1
        elif elem_type == "Condition":
            metrics["conditionCount"] += 1
        elif elem_type == "FunctionCall":
            metrics["functionCalls"] += 1

    elements.sort(key=lambda x: x["line"])
    return {"success": True, "elements": elements, "metrics": metrics}


if __name__ == "__main__":
    import sys
    code = sys.stdin.read()
    result = extract_normalized_ast(code)
    print(json.dumps(result))
