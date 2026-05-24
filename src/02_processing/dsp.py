import numpy as np
from scipy.signal import welch, iirnotch, butter, lfilter, firwin

FS = 256.0

# Taps count for FIR filter
TAPS = 129

def apply_filters(data, mode="BOTH", low_pass=100.0, high_pass=1.0, notch=60.0):
    """
    Applies Notch, FIR, and IIR bandpass filters dynamically.
    The default low-pass filter cutoff is 100.0 Hz.
    """
    nyq = 0.5 * FS
    
    # 1. Notch Filter Stage
    if notch > 0:
        b_notch, a_notch = iirnotch(notch, 30.0, FS)
        notched = lfilter(b_notch, a_notch, data, axis=0)
    else:
        notched = data.copy()
        
    # 2. FIR Stage
    if mode in ["BOTH", "ONLY_FIR"] and low_pass > high_pass:
        cutoff_high = min(low_pass, nyq - 1.0)
        coeffs = firwin(TAPS, [high_pass, cutoff_high], pass_zero=False, fs=FS)
        fir_denoised = lfilter(coeffs, 1.0, notched, axis=0)
    else:
        fir_denoised = notched
        
    # 3. IIR Stage
    if mode in ["BOTH", "ONLY_IIR"] and low_pass > high_pass:
        low = high_pass / nyq
        cutoff_high = min(low_pass, nyq - 1.0)
        high = cutoff_high / nyq
        b_band, a_band = butter(4, [low, high], btype='band')
        bandpassed = lfilter(b_band, a_band, fir_denoised, axis=0)
    else:
        bandpassed = fir_denoised
        
    return notched, fir_denoised, bandpassed

def compute_band_powers(filtered_data):
    """
    Extracts band powers using Welch's method and np.trapezoid.
    Uses a 2-second segment length (512 samples) and 1024-point FFT zero-padding
    for a smooth 0.25 Hz frequency resolution (401 points between 0 and 100 Hz).
    """
    freqs, psd = welch(filtered_data, fs=FS, nperseg=512, nfft=1024, axis=0)
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
        # Average only the frontal channels (AF7 and AF8, indices 1 and 2)
        # to focus on prefrontal activity and exclude jaw/muscle artifacts from TP9/TP10.
        powers[band] = np.mean(band_power[1:3])
        
    return powers, freqs, psd_avg, psd

def calculate_metrics(powers, raw_data, notched_data):
    """
    Calculates the alpha ratio and signal integrity based on >150uV threshold on AF7/AF8.
    Uses notched_data and a shorter window to avoid DC drift false positives.
    """
    alpha = powers.get('alpha', 0)
    beta = powers.get('beta', 0)
    alpha_ratio = alpha / beta if beta > 0 else 0
    
    
    # Check only the last 0.25 seconds across ALL sensors
    window = 64
    max_p2p = 0
    for ch in range(4):
        p2p = np.max(notched_data[-window:, ch]) - np.min(notched_data[-window:, ch])
        if p2p > max_p2p:
            max_p2p = p2p
    
    if max_p2p > 150:
        integrity = "RED"
    else:
        integrity = "GREEN"
        
    return {
        "alpha_ratio": alpha_ratio,
        "signal_integrity": integrity
    }
