import time
import numpy as np
import pylsl
import sys

def main():
    # Create stream info for mock Muse EEG
    # Name: Muse, Type: EEG, Channels: 4, Rate: 256Hz, Format: float32, Source: emulator123
    info = pylsl.StreamInfo('Muse', 'EEG', 4, 256, 'float32', 'emulator123')
    
    # Append some meta-data to match standard Muse LSL format if needed
    channels = info.desc().append_child("channels")
    for ch in ["TP9", "AF7", "AF8", "TP10"]:
        channels.append_child("channel").append_child_value("label", ch)
        
    outlet = pylsl.StreamOutlet(info)
    
    print("Mock LSL stream running. Pushing simulated EEG data...")
    print("Press Ctrl+C to stop.")
    
    fs = 256.0
    start_time = pylsl.local_clock()
    
    try:
        while True:
            # Send 12 samples at a time to match typical Muse chunk size
            chunk_size = 12
            t = pylsl.local_clock() - start_time
            
            # Generate simulated waves
            # TP9 & TP10: High alpha (10Hz)
            # AF7 & AF8: High beta (20Hz)
            time_array = np.linspace(t, t + chunk_size/fs, chunk_size, endpoint=False)
            
            tp9 = 10 * np.sin(2 * np.pi * 10 * time_array) + np.random.normal(0, 1, chunk_size)
            tp10 = 10 * np.sin(2 * np.pi * 10 * time_array) + np.random.normal(0, 1, chunk_size)
            af7 = 5 * np.sin(2 * np.pi * 20 * time_array) + np.random.normal(0, 1, chunk_size)
            af8 = 5 * np.sin(2 * np.pi * 20 * time_array) + np.random.normal(0, 1, chunk_size)
            
            # Optionally simulate a huge blink artifact on AF7/AF8 every 5 seconds
            if int(t) % 5 == 0 and (t - int(t)) < 0.05:
                af7 += 150
                af8 += 150
                
            chunk = np.column_stack((tp9, af7, af8, tp10))
            
            # Push chunk
            outlet.push_chunk(chunk.tolist(), pylsl.local_clock())
            
            # Sleep roughly the duration of the chunk to simulate real-time
            time.sleep(chunk_size / fs)
            
    except KeyboardInterrupt:
        print("Emulator shutting down.")

if __name__ == '__main__':
    main()
