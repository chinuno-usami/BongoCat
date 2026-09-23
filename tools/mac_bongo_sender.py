#!/usr/bin/env python3
"""
BongoCat LAN Sync - macOS Standalone Sender

Captures global keyboard presses and mouse clicks on macOS via CoreGraphics EventTap,
and broadcasts them over LAN via UDP to Windows running Steam BongoCat.
Requires macOS Accessibility permissions (System Settings -> Privacy & Security -> Accessibility).
"""

import sys
import os
import argparse
import socket
import ctypes
from ctypes import c_void_p, c_uint32, c_int32, c_int64, CFUNCTYPE, c_char_p

# Load macOS CoreGraphics and CoreFoundation frameworks
try:
    cg = ctypes.cdll.LoadLibrary('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics')
    cf = ctypes.cdll.LoadLibrary('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
except Exception as e:
    print(f"[Error] Failed to load macOS frameworks: {e}")
    sys.exit(1)

# Event constants
kCGSessionEventTap = 1
kCGHeadInsertEventTap = 0
kCGEventTapOptionListenOnly = 1

kCGEventLeftMouseDown = 1
kCGEventRightMouseDown = 2
kCGEventKeyDown = 10
kCGEventOtherMouseDown = 25

EVENT_MASK = (1 << kCGEventKeyDown) | (1 << kCGEventLeftMouseDown) | (1 << kCGEventRightMouseDown) | (1 << kCGEventOtherMouseDown)
kCFRunLoopCommonModes = c_void_p.in_dll(cf, 'kCFRunLoopCommonModes')

# Define callback function signature
# CGEventRef callback(CGEventTapProxy proxy, CGEventType type, CGEventRef event, void *refcon)
CGEventTapCallBack = CFUNCTYPE(c_void_p, c_void_p, c_uint32, c_void_p, c_void_p)

# Global stats and network socket
sock = None
dest_addr = None
tap_counter = 0

def event_callback(proxy, event_type, event, refcon):
    global tap_counter
    try:
        # Send UDP TAP packet
        msg = b"TAP:1"
        sock.sendto(msg, dest_addr)
        tap_counter += 1
        print(f"\r[BongoSync] Sent tap #{tap_counter} to {dest_addr[0]}:{dest_addr[1]}", end="", flush=True)
    except Exception as ex:
        pass
    return event

cb_func = CGEventTapCallBack(event_callback)

def main():
    global sock, dest_addr

    parser = argparse.ArgumentParser(description="BongoCat macOS to Windows LAN Sync Sender")
    parser.add_argument("--ip", default="255.255.255.255", help="Target Windows IP or 255.255.255.255 for broadcast (default: broadcast)")
    parser.add_argument("--port", type=int, default=39824, help="Target UDP port (default: 39824)")
    args = parser.parse_args()

    dest_addr = (args.ip, args.port)

    # Setup UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)

    print("==================================================")
    print("  BongoCat LAN Sync - macOS Sender")
    print(f"  Target: {dest_addr[0]}:{dest_addr[1]}")
    print("  Capturing: Keyboard & Mouse Clicks")
    print("  Note: Requires Accessibility permission.")
    print("==================================================")

    # Configure CGEventTapCreate
    cg.CGEventTapCreate.restype = c_void_p
    cg.CGEventTapCreate.argtypes = [c_uint32, c_uint32, c_uint32, c_int64, CGEventTapCallBack, c_void_p]

    port_ref = cg.CGEventTapCreate(
        kCGSessionEventTap,
        kCGHeadInsertEventTap,
        kCGEventTapOptionListenOnly,
        c_int64(EVENT_MASK),
        cb_func,
        None
    )

    if not port_ref:
        print("\n[Error] Unable to create macOS Event Tap!")
        print("Please ensure your Terminal has Accessibility permission:")
        print("System Settings -> Privacy & Security -> Accessibility -> Enable Terminal / Python")
        sys.exit(1)

    # Setup RunLoop source
    cf.CFMachPortCreateRunLoopSource.restype = c_void_p
    cf.CFMachPortCreateRunLoopSource.argtypes = [c_void_p, c_void_p, c_int32]
    run_loop_source = cf.CFMachPortCreateRunLoopSource(None, port_ref, 0)

    cf.CFRunLoopGetCurrent.restype = c_void_p
    loop = cf.CFRunLoopGetCurrent()

    cf.CFRunLoopAddSource.argtypes = [c_void_p, c_void_p, c_void_p]
    cf.CFRunLoopAddSource(loop, run_loop_source, kCFRunLoopCommonModes)

    cg.CGEventTapEnable.argtypes = [c_void_p, c_int32]
    cg.CGEventTapEnable(port_ref, 1)

    print("[BongoSync] Listening for inputs... Start typing to sync!")
    try:
        cf.CFRunLoopRun()
    except KeyboardInterrupt:
        print("\n[BongoSync] Sender stopped.")

if __name__ == "__main__":
    main()
