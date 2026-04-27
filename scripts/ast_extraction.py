import json
from graphify.extract import collect_files, extract
from pathlib import Path

# Load detected files
detect_path = Path('graphify-out/.graphify_detect.json')
if not detect_path.exists():
    print("Detection file not found.")
    exit(1)

detect = json.loads(detect_path.read_text())
code_files = detect.get('files', {}).get('code', [])

# Collect files
collected_files = []
for f in code_files:
    p = Path(f)
    collected_files.extend(collect_files(p) if p.is_dir() else [p])

# Extract AST
if collected_files:
    result = extract(collected_files)
    output_path = Path('graphify-out/.graphify_ast.json')
    output_path.write_text(json.dumps(result, indent=2))
    print(f"AST: {len(result['nodes'])} nodes, {len(result['edges'])} edges")
else:
    print("No code files found for AST extraction.")