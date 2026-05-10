import numpy as np

class RingBuffer:
    """
    A 2-second rolling ring buffer for EEG data (512 samples at 256Hz).
    """
    def __init__(self, size=512, channels=4):
        self.size = size
        self.channels = channels
        self.data = np.zeros((size, channels))
        self.index = 0
        self.is_full = False
        
    def append(self, chunk):
        if len(chunk) == 0:
            return
            
        chunk_arr = np.array(chunk)
        chunk_len = len(chunk_arr)
        
        if chunk_len >= self.size:
            self.data = chunk_arr[-self.size:]
            self.index = 0
            self.is_full = True
            return
            
        end_idx = self.index + chunk_len
        if end_idx <= self.size:
            self.data[self.index:end_idx] = chunk_arr
        else:
            overflow = end_idx - self.size
            self.data[self.index:self.size] = chunk_arr[:-overflow]
            self.data[0:overflow] = chunk_arr[-overflow:]
            self.is_full = True
            
        self.index = (self.index + chunk_len) % self.size
        if self.index == 0:
            self.is_full = True
            
    def get_data(self):
        """
        Returns the data chronologically if the buffer is full.
        """
        if not self.is_full:
            return None
        return np.concatenate((self.data[self.index:], self.data[:self.index]))
