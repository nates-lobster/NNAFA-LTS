import struct
import os

def get_exe_arch(path):
    if not os.path.exists(path):
        return "File not found"
    
    with open(path, 'rb') as f:
        # Check DOS header
        if f.read(2) != b'MZ':
            return "Not a valid EXE"
        
        # Seek to PE header offset
        f.seek(60)
        pe_offset = struct.unpack('<I', f.read(4))[0]
        
        # Seek to PE header
        f.seek(pe_offset)
        if f.read(4) != b'PE\0\0':
            return "Not a valid PE"
        
        # Read machine type
        machine = struct.unpack('<H', f.read(2))[0]
        if machine == 0x14c:
            return "32-bit (x86)"
        elif machine == 0x8664:
            return "64-bit (x64)"
        else:
            return f"Unknown machine type: {hex(machine)}"

if __name__ == "__main__":
    path = "M:/Muse Project/BlueMuse/LSLBridge/bin/Debug/LSLBridge.exe"
    print(f"LSLBridge.exe architecture: {get_exe_arch(path)}")
