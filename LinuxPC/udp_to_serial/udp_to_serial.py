#!/usr/bin/python3
import serial
import time
seri = serial.Serial('/dev/ttyUSB0',timeout=0.1,baudrate=115200,dsrdtr = True)
seri.rtscts=False
seri.dsrdtr=False
seri.rts=False
seri.dtr=False
time.sleep(0.5)
seri.read(100)# dummy read to clear buffer

import socket
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(("0.0.0.0",2828))
while True:
    msg, cli_addr = sock.recvfrom(1024) 
    d=msg.decode("ascii").split()
    if len(d) == 4 and "v"==d[0]:
        f=[float(dd) for dd in d[1:] ]
        if min(f)<-1 : continue
        if 1<max(f) : continue
        out_str="v{} {} {}\n".format(*f)
        print(time.time())
        seri.write(out_str.encode("ascii") )
