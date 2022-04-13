from time import sleep_us, sleep_ms, sleep
from machine import Pin, PWM
import _thread
import math
import socket
import network

wlan = network.WLAN(network.STA_IF)
wlan.active(True)
print(wlan.scan())
wlan.connect('Apple Network 7b2e80', 'rosemarymckee')
print("connected")
UDP_IP = "192.168.0.122"
UDP_PORT = 8080
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) # UDP
sock.bind((UDP_IP, UDP_PORT))

# Servo motor system constants
# Top Servo
TOP_RADIUS = 9
TOP_MIN_DUTY = 500000
TOP_MAX_DUTY = 2500000
TOP_START_POS = 600000
# Side Servo
SIDE_RADIUS = 7.75
SIDE_MIN_DUTY = 500000
SIDE_MAX_DUTY = 2500000
SIDE_START_POS = 1500000

# Initialize parameters and PWM pins for servo motors
# Top Servo
topCurrentPos = 600000
topMoveTime = 1
topPos = 0
pwmTop = PWM(Pin(19))
pwmTop.freq(333)
# Side Servo
sideCurrentPos = 1500000
sideMoveTime = 1
sidePos = 0
pwmSide = PWM(Pin(16))
pwmSide.freq(333)


def setServoPos(dist: float, radius: float, MIN_DUTY: int, MAX_DUTY: int, START_POS: int) -> float:
    """ This function uses the input parameters to calculate the PWM duty cycle
    in order for the servo motor to travel the specified distance dist.
    """
    dutyCyclePerDegree = (MAX_DUTY - MIN_DUTY) / 180
    degreesOfRotation = (dist / (2 * math.pi * radius)) * 360
    newPos = math.floor(dutyCyclePerDegree * degreesOfRotation + START_POS)
    return newPos

def setTopServoSpeed(moveTime: float, currentPos: float, finalPos: float) -> int:
    """ This function takes speed and a new duty cycle that corresponds to a specified
    displacement from the current rotor position and outputs a time delay in uSec. This time
    delay will be added after each increment of the duty cycle until the servo has reach
    the final duty cycle. Therefore, the speed is controlled by setting the time delay
    between each increment
    """
    #AGFRC motor 1.25msec/degree
    angle = (abs(finalPos-currentPos))/((TOP_MAX_DUTY - TOP_MIN_DUTY) / 180)
    servoDelay = ((moveTime-(0.00125*angle))/abs(finalPos-currentPos)) * 1000000
    return math.floor(servoDelay)

def setSideServoSpeed(moveTime: float, currentPos: float, finalPos: float) -> int:
    """ This function takes speed and a new duty cycle that corresponds to a specified
    displacement from the current rotor position and outputs a time delay in uSec. This time
    delay will be added after each increment of the duty cycle until the servo has reach
    the final duty cycle. Therefore, the speed is controlled by setting the time delay
    between each increment
    """
    #MC4 motor 2.5msec/degree
    angle = (abs(finalPos-currentPos))/((SIDE_MAX_DUTY - SIDE_MIN_DUTY) / 180)
    servoDelay = ((moveTime-(0.0025*angle))/abs(finalPos-currentPos)) * 1000000
    return math.floor(servoDelay)

def setSideServoSpeedDep(sideDutyCycleSteps: float, topDutyCycleSteps: float, topServoDelay: int) -> int:
    """ This function takes the top servo motors speed, the top servo motors
    delay, and a new duty cycle for the side servo motor that corresponds to
    a specified displacement from the current rotor position. Using these
    parameters it outputs a time delay in uSec that will control the side
    servo motors speed so that both the top and side servo motors reach the
    final position at the same time. This time delay will be added after each
    increment of the duty cycle until the servo has reach the final duty cycle.
    Therefore, the speed is controlled by setting the time delay between each increment.
    """
    topTime = topDutyCycleSteps*topServoDelay/1000000
    angle = (sideDutyCycleSteps-500)/((MAX_DUTY - MIN_DUTY) / 180)
    servoDelay = ((topTime-1.6*(angle/180))/(sideDutyCycleSteps-500)) * 1000000
    return math.floor(servoDelay)

def sideServoForward_thread(pwmSide, sideCurrentPos, sideFinalPos, sideServoDelay) -> None:
    from time import sleep_us
    from machine import PWM
    while True:
        for pos in range(sideCurrentPos, sideFinalPos, 2000):
            pwmSide.duty_ns(pos)
            sleep_us(sideServoDelay)
        break

def sideServoBackward_thread(pwmSide, sideCurrentPos, sideFinalPos, sideServoDelay) -> None:
    from time import sleep_us
    from machine import PWM
    while True:
        for pos in range(sideCurrentPos, sideFinalPos, -2000):
            pwmSide.duty_ns(pos)
            sleep_us(sideServoDelay)
        break

while True:
    Unitydata, addr = sock.recvfrom(1024) # buffer size is 1024 bytes
    topPos = float(Unitydata.decode("utf-8"))
    latestTopPos = setServoPos(topPos, TOP_RADIUS, TOP_MIN_DUTY, TOP_MAX_DUTY, TOP_START_POS)
    print(latestTopPos)
    print(topCurrentPos)
    if topCurrentPos != latestTopPos:
        topFinalPos = latestTopPos
        topServoDelay = setTopServoSpeed(topMoveTime, topCurrentPos, topFinalPos)
        sidePos = topPos/2
        sideFinalPos = setServoPos(sidePos, SIDE_RADIUS, SIDE_MIN_DUTY, SIDE_MAX_DUTY, SIDE_START_POS)
        sideServoDelay = setSideServoSpeed(sideMoveTime, sideCurrentPos, sideFinalPos)
        if topCurrentPos < topFinalPos:
            # Move forward
            _thread.start_new_thread(sideServoForward_thread, (pwmSide, sideCurrentPos, sideFinalPos, sideServoDelay))
            for pos in range(topCurrentPos, topFinalPos, 2000):
                pwmTop.duty_ns(pos)
                sleep_us(topServoDelay)
        elif topCurrentPos > topFinalPos:
            # Move backward
            _thread.start_new_thread(sideServoBackward_thread, (pwmSide, sideCurrentPos, sideFinalPos, sideServoDelay))
            for pos in range(topCurrentPos, topFinalPos, -2000):
                pwmTop.duty_ns(pos)
                sleep_us(topServoDelay)
        # Update current positions 
        topCurrentPos = topFinalPos
        sideCurrentPos = sideFinalPos
        



 

