import pylsl

class EEGStream:
    def __init__(self, stream_name="Muse", chunk_size=12):
        self.stream_name = stream_name
        self.chunk_size = chunk_size
        self.inlet = None
        
    def connect(self):
        print(f"Resolving {self.stream_name} stream...")
        streams = pylsl.resolve_byprop('type', 'EEG')
        if not streams:
            raise RuntimeError("No EEG stream found. Ensure BlueMuse is running.")
        self.inlet = pylsl.StreamInlet(streams[0], max_chunklen=self.chunk_size)
        print("Connected to stream.")
        
    def get_chunk(self):
        if not self.inlet:
            self.connect()
        chunk, timestamps = self.inlet.pull_chunk(timeout=1.0, max_samples=self.chunk_size)
        
        # BlueMuse broadcasts a 5th AUX channel. We only need the first 4 (TP9, AF7, AF8, TP10).
        if chunk and len(chunk[0]) > 4:
            chunk = [sample[:4] for sample in chunk]
            
        return chunk, timestamps
        
    def get_local_time(self):
        return pylsl.local_clock()
