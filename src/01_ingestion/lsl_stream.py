import pylsl
import logging

logger = logging.getLogger("lsl_stream")

class EEGStream:
    def __init__(self, stream_name="Muse", chunk_size=12):
        self.stream_name = stream_name
        self.chunk_size = chunk_size
        self.inlet = None
        
    def connect(self, timeout=1.0):
        logger.info(f"LSL: Resolving '{self.stream_name}' stream (type: EEG)...")
        try:
            # First try resolving by the exact stream name
            streams = pylsl.resolve_byprop('name', self.stream_name, timeout=timeout)
            if not streams:
                logger.warning(f"LSL: No stream named '{self.stream_name}' found. Trying fallback to type 'EEG'...")
                streams = pylsl.resolve_byprop('type', 'EEG', timeout=timeout)
                
            if not streams:
                logger.error("LSL: No EEG stream found on the network.")
                return False
                
            self.inlet = pylsl.StreamInlet(streams[0], max_chunklen=self.chunk_size)
            logger.info(f"LSL: Connected to stream '{streams[0].name()}' (type: '{streams[0].type()}') at {streams[0].nominal_srate()}Hz.")
            return True
        except Exception as e:
            logger.exception("LSL: Error during stream resolution:")
            return False
        
    def get_chunk(self):
        if not self.inlet:
            return None, None
        try:
            chunk, timestamps = self.inlet.pull_chunk(timeout=0.0, max_samples=self.chunk_size)
            
            # BlueMuse/Brainflow might broadcast an AUX channel. We only need the first 4 (TP9, AF7, AF8, TP10).
            if chunk and len(chunk[0]) > 4:
                chunk = [sample[:4] for sample in chunk]
                
            return chunk, timestamps
        except Exception as e:
            logger.exception("LSL: Error pulling chunk:")
            self.inlet = None
            return None, None
        
    def get_local_time(self):
        return pylsl.local_clock()

