import argparse
import zmq
import time


parser = argparse.ArgumentParser(description='zeromq server/client')
parser.add_argument('--bar')
args = parser.parse_args()

context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.connect('tcp://127.0.0.1:5555')
while True:
    socket.send_string('emotion plz')
    msg = socket.recv()
    print(msg)
    time.sleep(1)
