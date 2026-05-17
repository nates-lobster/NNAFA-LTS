import argparse
import time
import numpy as np
from brainflow.board_shim import BoardShim, BrainFlowInputParams, BoardIds
from pylsl import StreamInfo, StreamOutlet, local_clock

def main():
    parser = argparse.ArgumentParser(description="Brainflow to LSL Bridge for Muse 2/S")
    parser.add_argument('--board-id', type=int, 
                        help='Board ID. Muse 2 (Native): 38, Muse 2 (BLED): 22, Muse S (Native): 39, Muse S (BLED): 38', 
                        default=BoardIds.MUSE_2_BOARD.value)
    parser.add_argument('--serial-port', type=str, help='Serial port for BLED112 dongle (e.g. COM3)', default='')
    parser.add_argument('--mac-address', type=str, help='MAC address of your Muse', default='')
    args = parser.parse_args()

    BoardShim.enable_dev_board_logger()

    params = BrainFlowInputParams()
    params.serial_port = args.serial_port
    params.mac_address = args.mac_address

    print("[STATUS] INITIALIZING")
    board = BoardShim(args.board_id, params)
    
    try:
        print(f"[STATUS] SEARCHING (Board {args.board_id})")
        board.prepare_session()
        board.start_stream()
        print("[STATUS] CONNECTED")

        # LSL Setup
        eeg_channels = board.get_eeg_channels(args.board_id)
        sampling_rate = board.get_sampling_rate(args.board_id)
        
        info = StreamInfo('Muse', 'EEG', 5, sampling_rate, 'float32', 'brainflow_bridge_001')
        
        channels = info.desc().append_child("channels")
        for label in ["TP9", "AF7", "AF8", "TP10", "AUX"]:
            channels.append_child("channel").append_child_value("label", label).append_child_value("unit", "microvolts").append_child_value("type", "EEG")

        outlet = StreamOutlet(info)
        
        # Pull from Brainflow and Push to LSL
        while True:
            data = board.get_board_data()
            if data.any():
                eeg_data = data[eeg_channels]
                num_samples = eeg_data.shape[1]
                lsl_payload = np.zeros((5, num_samples))
                rows_to_copy = min(5, eeg_data.shape[0])
                lsl_payload[:rows_to_copy, :] = eeg_data[:rows_to_copy, :]
                
                samples = lsl_payload.T.tolist()
                for sample in samples:
                    outlet.push_sample(sample)
            
            time.sleep(0.01)

    except Exception as e:
        print(f"[STATUS] ERROR: {str(e)}")
        # If it's a timeout, give a friendly hint
        if "timeout" in str(e).lower():
            print("HINT: Ensure Muse is in pairing mode (pulsing light) and no other apps are using it.")
    finally:
        if board.is_prepared():
            board.stop_stream()
            board.release_session()
        print("[STATUS] DISCONNECTED")

if __name__ == "__main__":
    main()
