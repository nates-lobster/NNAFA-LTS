import os
import shutil
from datetime import datetime

def main():
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    log_file = os.path.join(base_dir, "brainflow_bridge.log")
    archive_dir = os.path.join(base_dir, "logs", "archive")

    print("=========================================")
    print("      NNAFA Log Management Utility       ")
    print("=========================================")
    print(f"Log File: {log_file}")
    print(f"Archive Folder: {archive_dir}")
    print("-----------------------------------------")
    print("1. Clear/Truncate current log file")
    print("2. Archive current log file and clear it")
    print("3. View log file size and status")
    print("4. Exit")
    print("=========================================")
    
    choice = input("Enter choice (1-4): ").strip()
    
    if choice == '1':
        if os.path.exists(log_file):
            with open(log_file, 'w', encoding='utf-8') as f:
                f.truncate()
            print("\n[SUCCESS] Log file cleared successfully.")
        else:
            print("\n[INFO] Log file does not exist yet.")
            
    elif choice == '2':
        if os.path.exists(log_file) and os.path.getsize(log_file) > 0:
            os.makedirs(archive_dir, exist_ok=True)
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            archive_file = os.path.join(archive_dir, f"brainflow_bridge_{timestamp}.log")
            
            # Copy to archive
            shutil.copy2(log_file, archive_file)
            
            # Truncate original
            with open(log_file, 'w', encoding='utf-8') as f:
                f.truncate()
                
            print(f"\n[SUCCESS] Log archived to: {archive_file}")
            print("[SUCCESS] Original log file cleared.")
        else:
            print("\n[WARNING] Log file is empty or does not exist. Nothing to archive.")
            
    elif choice == '3':
        if os.path.exists(log_file):
            size_kb = os.path.getsize(log_file) / 1024.0
            print(f"\n[STATUS] Log file size: {size_kb:.2f} KB")
        else:
            print("\n[STATUS] Log file does not exist yet.")
            
        if os.path.exists(archive_dir):
            archives = os.listdir(archive_dir)
            print(f"[STATUS] Archived logs found: {len(archives)}")
        else:
            print("[STATUS] No archives directory exists yet.")
            
    else:
        print("\nExiting utility.")

if __name__ == "__main__":
    main()
