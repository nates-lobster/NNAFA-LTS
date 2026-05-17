import pylsl

class EEGStream:
    def __init__(self, stream_name="Muse", chunk_size=12):
        self.stream_name = stream_name
        self.chunk_size = chunk_size
        self.inlet = None
        
    def connect(self, timeout=1.0):
        print(f"LSL: Resolving '{self.stream_name}' stream (type: EEG)...")
        try:
            streams = pylsl.resolve_byprop('type', 'EEG', timeout=timeout)
            if not streams:
                print(f"LSL: No '{self.stream_name}' stream found.")
                return False
            self.inlet = pylsl.StreamInlet(streams[0], max_chunklen=self.chunk_size)
            print(f"LSL: Connected to stream '{streams[0].name()}' at {streams[0].nominal_srate()}Hz.")
            return True
        except Exception as e:
            print(f"LSL: Error during resolution: {e}")
            return False
        
    def get_chunk(self):
        if not self.inlet:
            return None, None
        try:
            chunk, timestamps = self.inlet.pull_chunk(timeout=0.0, max_samples=self.chunk_size)
            
            # BlueMuse broadcasts a 5th AUX channel. We only need the first 4 (TP9, AF7, AF8, TP10).
            if chunk and len(chunk[0]) > 4:
                chunk = [sample[:4] for sample in chunk]
                
            return chunk, timestamps
        except Exception as e:
            print(f"LSL: Error pulling chunk: {e}")
            self.inlet = None
            return None, None
        
    def get_local_time(self):
        return pylsl.local_clock()
