import argparse
import time
import numpy as np
from brainflow.board_shim import BoardShim, BrainFlowInputParams, BoardIds
from pylsl import StreamInfo, StreamOutlet, local_clock
import sys
import signal
import logging
import os

# Configure logging
log_file = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "brainflow_bridge.log")
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s [%(levelname)s] (%(filename)s:%(lineno)d) - %(message)s',
    handlers=[
        logging.FileHandler(log_file, mode='a', encoding='utf-8')
    ]
)

running = True


def handle_signal(sig, frame):
    global running
    logging.info(f"Signal {sig} received. Requesting graceful shutdown...")
    print(f"[STATUS] TERMINATING (Signal {sig})")
    running = False

signal.signal(signal.SIGINT, handle_signal)
signal.signal(signal.SIGTERM, handle_signal)
if sys.platform == "win32":
    signal.signal(signal.SIGBREAK, handle_signal)

def main():
    # Force line-buffering on stdout/stderr to ensure real-time UI updates in C#
    sys.stdout.reconfigure(line_buffering=True)
    sys.stderr.reconfigure(line_buffering=True)

    logging.info("Starting Brainflow to LSL Bridge...")
    parser = argparse.ArgumentParser(description="Brainflow to LSL Bridge for Muse 2/S")
    parser.add_argument('--board-id', type=int, 
                        help='Board ID. Muse 2 (Native): 38, Muse 2 (BLED): 22, Muse S (Native): 39, Muse S (BLED): 38', 
                        default=BoardIds.MUSE_2_BOARD.value)
    parser.add_argument('--serial-port', type=str, help='Serial port for BLED112 dongle (e.g. COM3)', default='')
    parser.add_argument('--mac-address', type=str, help='MAC address of your Muse', default='')
    args = parser.parse_args()

    logging.info(f"Parsed arguments: board_id={args.board_id}, serial_port='{args.serial_port}', mac_address='{args.mac_address}'")

    # Redirect Brainflow native logs to a file to prevent them from flooding sys.stderr,
    # as the C# frontend UI treats any standard error output as a connection error.
    BoardShim.set_log_file("brainflow_native.log")
    BoardShim.enable_dev_board_logger()

    params = BrainFlowInputParams()
    params.serial_port = args.serial_port
    params.mac_address = args.mac_address

    print("[STATUS] INITIALIZING", flush=True)
    logging.info("Initializing BoardShim...")
    board = BoardShim(args.board_id, params)
    
    try:
        print(f"[STATUS] SEARCHING (Board {args.board_id})", flush=True)
        logging.info("Preparing session...")
        board.prepare_session()
        logging.info("Session prepared. Starting stream...")
        board.start_stream()
        print("[STATUS] CONNECTED", flush=True)
        logging.info("Brainflow stream started successfully!")

        # LSL Setup
        eeg_channels = board.get_eeg_channels(args.board_id)
        sampling_rate = board.get_sampling_rate(args.board_id)
        logging.info(f"Board EEG channels: {eeg_channels}, Sampling rate: {sampling_rate}Hz")
        
        info = StreamInfo('Muse', 'EEG', 5, sampling_rate, 'float32', 'brainflow_bridge_001')
        
        channels = info.desc().append_child("channels")
        for label in ["TP9", "AF7", "AF8", "TP10", "AUX"]:
            channels.append_child("channel").append_child_value("label", label).append_child_value("unit", "microvolts").append_child_value("type", "EEG")

        logging.info("Initializing LSL StreamOutlet...")
        outlet = StreamOutlet(info)
        logging.info("LSL StreamOutlet created.")
        
        # Pull from Brainflow and Push to LSL
        sample_count = 0
        last_log_time = time.time()
        
        while running:
            data = board.get_board_data()
            if data is not None and data.shape[1] > 0:
                eeg_data = data[eeg_channels]
                num_samples = eeg_data.shape[1]
                lsl_payload = np.zeros((5, num_samples))
                rows_to_copy = min(5, eeg_data.shape[0])
                lsl_payload[:rows_to_copy, :] = eeg_data[:rows_to_copy, :]
                
                samples = lsl_payload.T.tolist()
                for sample in samples:
                    outlet.push_sample(sample)
                
                sample_count += num_samples
                
                # Periodically log statistics in debug file
                now = time.time()
                if now - last_log_time > 5.0:
                    logging.debug(f"Pushed {sample_count} samples so far. Current chunk size: {num_samples} samples.")
                    last_log_time = now
            
            time.sleep(0.01)

    except Exception as e:
        logging.exception("Fatal error in brainflow bridge loop:")
        print(f"[STATUS] ERROR: {str(e)}", flush=True)
        import traceback
        sys.stderr.write(traceback.format_exc())
        sys.stderr.flush()
        if "timeout" in str(e).lower():
            print("HINT: Ensure Muse is in pairing mode (pulsing light) and no other apps are using it.", flush=True)
    finally:
        logging.info("Exiting main loop, performing clean up...")
        if board.is_prepared():
            logging.info("Stopping stream and releasing session...")
            board.stop_stream()
            board.release_session()
            logging.info("Session released.")
        print("[STATUS] DISCONNECTED", flush=True)
        logging.info("Graceful shutdown complete.")

if __name__ == "__main__":
    main()

