#!/usr/bin/env python3
"""
Klyze.gg Local Development Server
Simple HTTP server for testing the website locally
"""

import http.server
import socketserver
import os
import sys

# Configuration
PORT = 8000
DIRECTORY = os.path.dirname(os.path.abspath(__file__))

class MyHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)
    
    def end_headers(self):
        # Add CORS headers for local development
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        super().end_headers()

def main():
    try:
        with socketserver.TCPServer(("", PORT), MyHTTPRequestHandler) as httpd:
            print(f"╔═══════════════════════════════════════════════════════╗")
            print(f"║           Klyze.gg Development Server                 ║")
            print(f"╠═══════════════════════════════════════════════════════╣")
            print(f"║  Server running at: http://localhost:{PORT}            ║")
            print(f"║  Directory: {DIRECTORY[:35]:<35} ║")
            print(f"║                                                       ║")
            print(f"║  Press Ctrl+C to stop the server                      ║")
            print(f"╚═══════════════════════════════════════════════════════╝")
            print()
            print(f"[INFO] Serving files from: {DIRECTORY}")
            print(f"[INFO] Main page: http://localhost:{PORT}/klyze.html")
            print()
            
            httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n\n[INFO] Server stopped by user")
        sys.exit(0)
    except OSError as e:
        if e.errno == 10048:  # Port already in use on Windows
            print(f"\n[ERROR] Port {PORT} is already in use!")
            print(f"[INFO] Try closing other applications or use a different port")
        else:
            print(f"\n[ERROR] {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
