import zmq

context = zmq.Context()
socket = context.socket(zmq.REP)
socket.bind('tcp://127.0.0.1:5555')
while True:
    msg = socket.recv_string()
    print(msg)
    socket.send_string('thanks')