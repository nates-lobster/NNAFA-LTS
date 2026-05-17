import asyncio
import websockets
import sys
import os
import time

base_dir = os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
sys.path.append(os.path.join(base_dir, 'src', '01_ingestion'))
sys.path.append(os.path.join(base_dir, 'src', '02_processing'))
sys.path.append(os.path.join(base_dir, 'src', '03_bridge', 'generated'))

import lsl_stream
import buffer
import dsp
import telemetry_v1_pb2
import state_v1_pb2
from collections import deque

# Global settings and state
CURRENT_MODE = "BOTH"
RATIO_HISTORY = deque(maxlen=200) # Max 10s @ 20Hz
CURRENT_SMOOTHING_S = 1.0
CALIBRATION_BUFFER = []
TARGET_RATIO = 1.0
CALIBRATION_TOTAL_SAMPLES = 600 # 30s @ 20Hz
IS_CALIBRATING = False

async def listen_for_commands(websocket):
    global CURRENT_MODE, IS_CALIBRATING, CALIBRATION_BUFFER
    try:
        async for message in websocket:
            request = state_v1_pb2.StateRequest()
            request.ParseFromString(message)
            
            # Filter mode
            if request.settings.filter_mode == state_v1_pb2.FILTER_BOTH:
                CURRENT_MODE = "BOTH"
            elif request.settings.filter_mode == state_v1_pb2.FILTER_ONLY_IIR:
                CURRENT_MODE = "ONLY_IIR"
            elif request.settings.filter_mode == state_v1_pb2.FILTER_ONLY_FIR:
                CURRENT_MODE = "ONLY_FIR"
            
            # Smoothing
            if request.settings.smoothing_window_s > 0:
                global CURRENT_SMOOTHING_S
                CURRENT_SMOOTHING_S = request.settings.smoothing_window_s
            
            if request.settings.reset_buffers:
                global RATIO_HISTORY, CALIBRATION_BUFFER
                RATIO_HISTORY.clear()
                CALIBRATION_BUFFER = []
                print("Session buffers reset.")
            
            # State transitions
            if request.target_state == state_v1_pb2.STATE_CALIBRATING:
                IS_CALIBRATING = True
                CALIBRATION_BUFFER = []
                print("Starting 30s calibration...")
            
            print(f"WS: Command received. Mode: {CURRENT_MODE}, Calibrating: {IS_CALIBRATING}")
    except Exception as e:
        print(f"WS: Error in command listener: {e}")

async def lsl_connection_manager(stream):
    while True:
        if not stream.inlet:
            try:
                # Resolving stream in a background thread to avoid blocking the asyncio loop!
                await asyncio.to_thread(stream.connect, timeout=1.0)
            except Exception as e:
                print(f"LSL Resolve Task error: {e}")
        await asyncio.sleep(5.0)  # Retry resolution every 5 seconds

async def eeg_loop(websocket):
    global CURRENT_MODE, RATIO_HISTORY, CALIBRATION_BUFFER, TARGET_RATIO, IS_CALIBRATING, CURRENT_SMOOTHING_S
    stream = lsl_stream.EEGStream(chunk_size=12)
    ring = buffer.RingBuffer(size=512, channels=4)
    last_data_time = time.time()
    last_stall_warning = 0
    
    print(f"WS: Client connected from {websocket.remote_address}. Waiting for EEG data...")
    # Start command listener and LSL connection manager in background
    cmd_task = asyncio.create_task(listen_for_commands(websocket))
    conn_task = asyncio.create_task(lsl_connection_manager(stream))
    
    last_heartbeat = time.time()
    
    try:
        while True:
            chunk, timestamps = stream.get_chunk()
            payload = None
            
            if chunk:
                ring.append(chunk)
                data = ring.get_data()
                
                if data is not None:
                    last_data_time = time.time()
                    # Pure DSP
                    notched, fir_denoised, filtered = dsp.apply_filters(data, mode=CURRENT_MODE)
                    powers, freqs, psd_avg, psd_all = dsp.compute_band_powers(filtered)
                    metrics = dsp.calculate_metrics(powers, data, notched)
                    
                    # Create Protobuf Payload
                    payload = telemetry_v1_pb2.TelemetryPayload()
                    payload.is_stalled = False
                    payload.timestamp_ms = stream.get_local_time() * 1000
                    
                    # Use latest filtered sample for "clean" channel data
                    payload.channels.tp9 = filtered[-1, 0]
                    payload.channels.af7 = filtered[-1, 1]
                    payload.channels.af8 = filtered[-1, 2]
                    payload.channels.tp10 = filtered[-1, 3]

                    # Latest raw sample
                    payload.raw_channels.tp9 = data[-1, 0]
                    payload.raw_channels.af7 = data[-1, 1]
                    payload.raw_channels.af8 = data[-1, 2]
                    payload.raw_channels.tp10 = data[-1, 3]

                    # Latest notched sample
                    payload.notched_channels.tp9 = notched[-1, 0]
                    payload.notched_channels.af7 = notched[-1, 1]
                    payload.notched_channels.af8 = notched[-1, 2]
                    payload.notched_channels.tp10 = notched[-1, 3]
                    
                    # Latest FIR sample
                    payload.fir_channels.tp9 = fir_denoised[-1, 0]
                    payload.fir_channels.af7 = fir_denoised[-1, 1]
                    payload.fir_channels.af8 = fir_denoised[-1, 2]
                    payload.fir_channels.tp10 = fir_denoised[-1, 3]
                    
                    payload.band_power.delta = powers['delta']
                    payload.band_power.theta = powers['theta']
                    payload.band_power.alpha = powers['alpha']
                    payload.band_power.beta = powers['beta']
                    payload.band_power.gamma = powers['gamma']
                    
                    freq_limit_idx = freqs <= 100
                    payload.psd_freqs.extend(freqs[freq_limit_idx].tolist())
                    payload.psd_powers.extend(psd_avg[freq_limit_idx].tolist())
                    
                    # Per-channel PSDs
                    payload.psd_tp9.extend(psd_all[freq_limit_idx, 0].tolist())
                    payload.psd_af7.extend(psd_all[freq_limit_idx, 1].tolist())
                    payload.psd_af8.extend(psd_all[freq_limit_idx, 2].tolist())
                    payload.psd_tp10.extend(psd_all[freq_limit_idx, 3].tolist())
                    
                    payload.metrics.alpha_ratio = metrics['alpha_ratio']
                    
                    # Update History & Smoothing
                    RATIO_HISTORY.append(metrics['alpha_ratio'])
                    
                    # Calculate mean over current window
                    window_samples = max(1, int(CURRENT_SMOOTHING_S * 20))
                    recent_samples = list(RATIO_HISTORY)[-window_samples:]
                    smoothed_ratio = sum(recent_samples) / len(recent_samples)
                    
                    payload.smoothed_alpha_ratio = smoothed_ratio
                    
                    # Handle Calibration
                    if IS_CALIBRATING:
                        CALIBRATION_BUFFER.append(metrics['alpha_ratio'])
                        progress = len(CALIBRATION_BUFFER) / CALIBRATION_TOTAL_SAMPLES
                        payload.calibration_progress = progress
                        if len(CALIBRATION_BUFFER) >= CALIBRATION_TOTAL_SAMPLES:
                            # End calibration, set target ratio
                            # Target = Mean + 0.1 (make it slightly harder than baseline)
                            TARGET_RATIO = sum(CALIBRATION_BUFFER) / len(CALIBRATION_BUFFER) + 0.1
                            IS_CALIBRATING = False
                            print(f"Calibration complete. Target: {TARGET_RATIO:.2f}")
                    
                    if metrics['signal_integrity'] == "GREEN":
                        payload.metrics.signal_integrity = telemetry_v1_pb2.GREEN
                    elif metrics['signal_integrity'] == "RED":
                        payload.metrics.signal_integrity = telemetry_v1_pb2.RED
                    else:
                        payload.metrics.signal_integrity = telemetry_v1_pb2.YELLOW
            
            if payload is None:
                # No data this tick, check if we've stalled
                is_stalled = (time.time() - last_data_time) > 2.0
                payload = telemetry_v1_pb2.TelemetryPayload()
                payload.is_stalled = is_stalled
                payload.timestamp_ms = stream.get_local_time() * 1000
            
            if payload.is_stalled:
                now = time.time()
                if now - last_stall_warning > 5.0:
                    print("LSL: Stream stalled! Waiting for Muse stream...")
                    last_stall_warning = now
            
            await websocket.send(payload.SerializeToString())
            
            # Heartbeat every 5 seconds
            if time.time() - last_heartbeat > 5.0:
                print(f"WS: Connection alive. Streaming at ~20Hz. Smoothing: {CURRENT_SMOOTHING_S}s")
                last_heartbeat = time.time()
                
            await asyncio.sleep(0.05) # Cap at ~20Hz updates
    except websockets.exceptions.ConnectionClosed:
        print("Client disconnected.")
    except Exception as e:
        print(f"Error in eeg loop: {e}")
    finally:
        cmd_task.cancel()
        conn_task.cancel()

async def main():
    print("Starting WebSocket server on port 8765...")
    try:
        async with websockets.serve(eeg_loop, "127.0.0.1", 8765):
            print("Server is listening. Ready for C# Frontend connection.")
            await asyncio.Future()  # run forever
    except OSError as e:
        if e.errno == 10048:
            print("\n[CRITICAL ERROR] Port 8765 is already in use.")
            print("Action Required: An orphaned Python process is likely holding the port.")
            print("Fix: Use the 'Kill All Python' button in the C# UI or run: taskkill /F /IM python.exe\n")
        else:
            print(f"Server startup error: {e}")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("Server shutting down gracefully.")
