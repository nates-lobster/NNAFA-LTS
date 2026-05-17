import ctypes
import os
import sys

def test_dll(path):
    print(f"--- Testing: {path} ---")
    if not os.path.exists(path):
        print(f"FAILED: File does not exist at {path}")
        return
    
    try:
        # Load library
        lib = ctypes.WinDLL(path)
        print(f"SUCCESS: Loaded {path}")
        print(f"Handle: {lib._handle}")
    except Exception as e:
        print(f"FAILED to load {path}")
        print(f"Error: {e}")
        if "126" in str(e):
            print("Tip: Error 126 usually means a DEPENDENCY (like Visual C++ Redistributable) is missing, not the file itself.")
        elif "193" in str(e):
            print("Tip: Error 193 means architecture mismatch (trying to load 64-bit DLL in 32-bit Python or vice versa).")

if __name__ == "__main__":
    # Test both architectures
    base_path = "M:/Muse Project/BlueMuse/LSLBridge/Binaries"
    test_dll(os.path.join(base_path, "liblsl64.dll"))
    test_dll(os.path.join(base_path, "liblsl32.dll"))
    
    print("\n--- Python Info ---")
    print(f"Python version: {sys.version}")
    print(f"Python architecture: {'64-bit' if sys.maxsize > 2**32 else '32-bit'}")
