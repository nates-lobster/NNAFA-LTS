import numpy as np
from scipy.signal import welch, iirnotch, butter, lfilter, firwin

FS = 256.0

# Pre-calculate FIR coefficients (129 taps for 1-100Hz bandpass)
TAPS = 129
fir_coeffs = firwin(TAPS, [1.0, 100.0], pass_zero=False, fs=FS)

def apply_filters(data, mode="BOTH"):
    """
    Applies filters based on mode: BOTH, ONLY_IIR, ONLY_FIR.
    """
    nyq = 0.5 * FS
    b_notch, a_notch = iirnotch(60.0, 30.0, FS)
    notched = lfilter(b_notch, a_notch, data, axis=0)
    
    # FIR Stage
    if mode in ["BOTH", "ONLY_FIR"]:
        fir_denoised = lfilter(fir_coeffs, 1.0, notched, axis=0)
    else:
        fir_denoised = notched # Pass through
        
    # IIR Stage
    if mode in ["BOTH", "ONLY_IIR"]:
        low = 1.0 / nyq
        high = 100.0 / nyq
        b_band, a_band = butter(4, [low, high], btype='band')
        bandpassed = lfilter(b_band, a_band, fir_denoised, axis=0)
    else:
        bandpassed = fir_denoised # Pass through
    
    return notched, fir_denoised, bandpassed

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
        
    return powers, freqs, psd_avg, psd

def calculate_metrics(powers, raw_data, notched_data):
    """
    Calculates the alpha ratio and signal integrity based on >150uV threshold on AF7/AF8.
    Uses notched_data and a shorter window to avoid DC drift false positives.
    """
    alpha = powers.get('alpha', 0)
    beta = powers.get('beta', 0)
    alpha_ratio = alpha / beta if beta > 0 else 0
    
    # Check only the last 0.25 seconds (64 samples at 256Hz)
    window = 64
    af7 = notched_data[-window:, 1]
    af8 = notched_data[-window:, 2]
    
    p2p_af7 = np.max(af7) - np.min(af7)
    p2p_af8 = np.max(af8) - np.min(af8)
    
    if p2p_af7 > 150 or p2p_af8 > 150:
        integrity = "RED"
    else:
        integrity = "GREEN"
        
    return {
        "alpha_ratio": alpha_ratio,
        "signal_integrity": integrity
    }
