import sys
import os
import logging
import importlib.util

# Ensure project root is in PYTHONPATH
project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
if project_root not in sys.path:
    sys.path.append(project_root)

def import_module_from_path(module_name, file_path):
    spec = importlib.util.spec_from_file_location(module_name, file_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

# Import brainflow_lsl_bridge.main
bridge_path = os.path.join(project_root, 'src', '01_ingestion', 'brainflow_lsl_bridge.py')
bridge_module = import_module_from_path('brainflow_lsl_bridge', bridge_path)
bridge_main = bridge_module.main
print('brainflow_lsl_bridge import SUCCESS')

# Import lsl_stream.LSLStream
lsl_path = os.path.join(project_root, 'src', '01_ingestion', 'lsl_stream.py')
lsl_module = import_module_from_path('lsl_stream', lsl_path)
LSLStream = lsl_module.EEGStream
print('lsl_stream import SUCCESS')

print('All imports succeeded')
