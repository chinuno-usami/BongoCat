#!/usr/bin/env python3
"""
BongoCat LAN Sync - Windows Standalone Receiver (Option B)

For users who prefer NOT to modify Steam game files.
Listens on UDP port 39824 and sends safe, non-character virtual key signals (VK_NONAME = 0xFC)
which are detected by Steam BongoCat's WinKeyHook without typing characters into any active window.
"""

import sys
import socket
import ctypes
import time

PORT = 39824
VK_NONAME = 0xFC  # Steam WinKeyHook recognizes this, but Windows active apps produce NO character.

user32 = ctypes.windll.user32

def simulate_tap():
    # Key down
    user32.keybd_event(VK_NONAME, 0, 0, 0)
    # Key up
    user32.keybd_event(VK_NONAME, 0, 2, 0)  # KEYEVENTF_KEYUP = 2

def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    # Enable address reuse
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    
    try:
        sock.bind(("0.0.0.0", PORT))
    except Exception as e:
        print(f"[Error] Failed to bind to UDP port {PORT}: {e}")
        print("Please check if another program (or Steam BongoCat with Option A patch) is using port 39824.")
        sys.exit(1)

    print("==================================================")
    print("  BongoCat LAN Sync - Windows Receiver (Option B)")
    print(f"  Listening on UDP port: {PORT}")
    print("  Triggering non-character virtual key (VK_NONAME)")
    print("  Press Ctrl+C to stop.")
    print("==================================================")

    tap_count = 0
    try:
        while True:
            data, addr = sock.recvfrom(1024)
            if not data:
                continue

            msg = data.decode("utf-8", errors="ignore").strip()
            count = 1
            if msg.upper().startswith("TAP:"):
                try:
                    count = min(int(msg.split(":")[1]), 20)
                except Exception:
                    count = 1

            for _ in range(count):
                simulate_tap()
                tap_count += 1

            print(f"\r[BongoSync] Received tap from {addr[0]} | Total taps: {tap_count}", end="", flush=True)
    except KeyboardInterrupt:
        print("\n[BongoSync] Receiver stopped.")
    finally:
        sock.close()

if __name__ == "__main__":
    main()
