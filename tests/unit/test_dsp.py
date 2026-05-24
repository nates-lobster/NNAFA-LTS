import numpy as np
import sys
import os

base_dir = os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
sys.path.append(os.path.join(base_dir, 'src', '02_processing'))

import dsp

def test_dsp_pipeline():
    t = np.linspace(0, 2, 512, endpoint=False)
    alpha_wave = np.sin(2 * np.pi * 10 * t)
    noise = np.random.normal(0, 0.1, 512)
    
    signal = alpha_wave + noise
    data = np.column_stack([signal]*4)
    
    notched, fir_denoised, bandpassed = dsp.apply_filters(data)
    assert bandpassed.shape == (512, 4)
    
    powers, freqs, psd_avg, psd_all = dsp.compute_band_powers(bandpassed)
    assert 'alpha' in powers
    assert 'beta' in powers
    assert powers['alpha'] > powers['beta']
    
    metrics = dsp.calculate_metrics(powers, data, data)
    assert metrics['alpha_ratio'] > 0.0
    assert metrics['signal_integrity'] == "GREEN"
    
    print("All DSP unit tests passed deterministically.")

if __name__ == "__main__":
    test_dsp_pipeline()
