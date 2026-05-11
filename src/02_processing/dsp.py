import numpy as np
from scipy.signal import welch, iirnotch, butter, lfilter

FS = 256.0

def apply_filters(data):
    """
    Applies a 60Hz notch and 1-100Hz bandpass filter.
    data shape: (samples, channels)
    """
    nyq = 0.5 * FS
    b_notch, a_notch = iirnotch(60.0, 30.0, FS)
    notched = lfilter(b_notch, a_notch, data, axis=0)
    
    low = 1.0 / nyq
    high = 100.0 / nyq
    b_band, a_band = butter(4, [low, high], btype='band')
    bandpassed = lfilter(b_band, a_band, notched, axis=0)
    
    return notched, bandpassed

def compute_band_powers(filtered_data):
    """
    Extracts band powers using Welch's method and np.trapezoid.
    """
    freqs, psd = welch(filtered_data, fs=FS, nperseg=256, axis=0)
    psd_avg = np.mean(psd, axis=1)
    
    bands = {
        'delta': (1, 4),
        'theta': (4, 8),
        'alpha': (8, 12),
        'beta': (12, 30),
        'gamma': (30, 40)
    }
    
    powers = {}
    for band, (low, high) in bands.items():
        idx = np.logical_and(freqs >= low, freqs <= high)
        band_power = np.trapezoid(psd[idx], freqs[idx], axis=0)
        powers[band] = np.mean(band_power) # Average across 4 channels
        
    return powers, freqs, psd_avg

def calculate_metrics(powers, raw_data):
    """
    Calculates the alpha ratio and signal integrity based on >100uV threshold on AF7/AF8.
    Assumes channels are [TP9, AF7, AF8, TP10].
    """
    alpha = powers.get('alpha', 0)
    beta = powers.get('beta', 0)
    alpha_ratio = alpha / beta if beta > 0 else 0
    
    af7 = raw_data[:, 1]
    af8 = raw_data[:, 2]
    
    p2p_af7 = np.max(af7) - np.min(af7)
    p2p_af8 = np.max(af8) - np.min(af8)
    
    if p2p_af7 > 100 or p2p_af8 > 100:
        integrity = "RED"
    else:
        integrity = "GREEN"
        
    return {
        "alpha_ratio": alpha_ratio,
        "signal_integrity": integrity
    }
