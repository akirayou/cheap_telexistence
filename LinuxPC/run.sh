#!/bin/sh
sleep 1
cd /home/akria
/usr/local/bin/mjpg_streamer -o "output_http.so  -w /home/akria/www" -i "input_uvc.so -r 1600x1200 -f 8"  &
/usr/bin/python3 /home/akria/udp_to_serial/udp_to_serial.py  &
