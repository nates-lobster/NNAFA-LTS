import pylsl
import time

def list_streams():
    print("Searching for LSL streams for 5 seconds...")
    streams = pylsl.resolve_streams(wait_time=5.0)
    if not streams:
        print("No LSL streams found.")
        return
    
    print(f"Found {len(streams)} streams:")
    for s in streams:
        print(f" - Name: {s.name()}, Type: {s.type()}, Channels: {s.channel_count()}, Host: {s.hostname()}")

if __name__ == "__main__":
    list_streams()
