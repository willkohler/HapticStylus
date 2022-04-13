using System;
using System.Text;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net.Sockets;
using UnityEngine;

public class Hand : MonoBehaviour
{
    // Used to control animations
    private Animator animator;

    // Input to Visuals for tracking coordinates
    private Tracking PosCoords;

    // Create log file to record pen details such as coordinates
    private StreamWriter record;

    // IP address of the arduino. Used to send motor Position over UDP
    private string IP_address = "192.168.0.126";

    // Create Array to hold previous rotation values
    private float[] rotationHistory = new float[5];

    // Create Array to hold previous rotation values
    private float[] xPosHistory = new float[3];

    // Create Array to hold previous rotation values
    private float[] yPosHistory = new float[3];

    public double motorPosition = 0;

    // Start is called before the first frame update
    private void Start()
    {
        // Create animator object
        animator = GetComponent<Animator>();

        // Create NewBehaviourObject
        PosCoords = GetComponent<Tracking>();

        // Initialize log file
        record = File.AppendText("PenCoordiantes_Log.txt");

        // Set animator to disabled so that animation does not start automatically
        animator.enabled = false;
    }

    // If Objects make contact, grip animation
    private void OnTriggerEnter2D(Collider2D other)
    {
        animator.enabled = true;
        animator.SetBool("ObjectContact", true);
    }

    // If removing contact, grip release animation
    private void OnTriggerExit2D(Collider2D other)
    {
        animator.SetBool("ObjectContact", false);
    }

    // Update is called periodically to check for position
    // This function updates the hand position and the motor posiiton
    // It also logs the hand position to a file and sends the motor Position over UDP
    private void Update()
    {
        // Check if the aruco marker is being found
        // If it is, update hand position, update motor Position, log coordinates to log file
        if (!PosCoords.corners.Length.Equals(0))
        {
            // Normalize OpenCV coordinates to Unity Screen
            var UnityNormalizedCoordinates = NormalizeLocationCoords(PosCoords.corners[0][3].X, PosCoords.corners[0][3].Y);
            var UnityRotationValue = CalcRotationVal(PosCoords.corners);

            // Log coordinates
            LogCoordinates(UnityNormalizedCoordinates, UnityRotationValue, record);

            // Calculate position to set motor to
            motorPosition = CalculateMotorPosition(UnityNormalizedCoordinates.Item2);

            // Check if y coord is less than the counter surface value (-4.3). If it is, set to counter surface value
            if (UnityNormalizedCoordinates.Item2 < -4.3)
            {
                UnityNormalizedCoordinates.Item2 = (float)-4.3;
            }

            // Average last ROTATION_ARR_LEN values
            var averagedRotationValue = AverageRecentValues(UnityRotationValue, rotationHistory);
            var averagedX = AverageRecentValues(UnityNormalizedCoordinates.Item1, xPosHistory);
            var averagedY = AverageRecentValues(UnityNormalizedCoordinates.Item2, yPosHistory);

            // Set hand Position
            transform.position = new Vector3(averagedX, averagedY);
            transform.rotation = Quaternion.Euler(0, 0, averagedRotationValue);
        }

        // Send motor position over UDP
        SendMotorPositionOverUDP();
    }

    // Normalize X and Y coordinates to the Unity Screen Coordinates
    private (float, float) NormalizeLocationCoords(double Xval, double Yval)
    {
        // Calculate Normalized X value

        // X Coordinate Notes:
        // On Screen Unity coordinates X-range = -9.5 to 8.5
        // OpenCV Tracking coordinates for screen X-range = 250-1600

        double XLeftUnity = -9.5;
        double XRightUnity = 8.5;
        double XLeftOpenCV = 1600;
        double XRightOpenCV = 250;

        double XNormalized = ((Xval - XLeftOpenCV) * (XRightUnity - XLeftUnity) / (XRightOpenCV - XLeftOpenCV)) + XLeftUnity;

        // Calculate Normalized Y value

        // Y Coordinate Notes:
        // On Screen Unity coordinates Y-range = -5 to 5
        // OpenCV Tracking coordinates for screen Y-range = 850-350
        double YBottomtUnity = -5;
        double YTopUnity = 5;
        double YBottomOpenCV = 500;
        double YTopOpenCV = 0;

        double YNormalized = ((Yval - YBottomOpenCV) * (YTopUnity - YBottomtUnity) / (YTopOpenCV - YBottomOpenCV)) + YBottomtUnity;

        return ((float)XNormalized, (float)YNormalized);
    }

    // Calculate and Normalize Rotation value for Unity
    private float CalcRotationVal(OpenCvSharp.Point2f[][] CornerVals)
    {
        // Location Marker Note:
        // In reality: Marker size is 1.8 cm^2
        // In unity: If scalepl is pointing directly to right (horisontal to surface, hand upsidedown), rotation angle = 145
        // In unity: The rotation of the image moves in degrees. So if you want the scalpel to point straight up, rotation angle = 145 + 90

        // Find angle created by top two corners in reality. (0 degrees is when the scalpel points right (hand upsidedown) and is horizontal to the ground)
        var corner1 = CornerVals[0][0]; // Top corner closest to length of pen
        var corner2 = CornerVals[0][3]; // Top corner closest to flat hammerhead surface

        var xDist = corner2.X - corner1.X;
        var yDist = corner2.Y - corner1.Y;

        float angle = Mathf.Acos(xDist / (Mathf.Sqrt(xDist * xDist + yDist * yDist))) * 180 / Mathf.PI;

        // Actual angle depends on whether y value is neg or pos
        if (yDist < 0)
        {
            // scalpel pointing downwards
            angle = 360 - angle;
        }

        // Convert Actual angle to unity angle
        // Add 175 to calculated angle since: unity rotation of 145 is equal to real angle of 0 degrees
        angle = angle + 180;

        return angle;
    }

    // Function to shift coordinate and rotation array elements left by 1 and add latest value into the rightmost part of the array
    // Function then returns average of all elements to reduce shaking of the hand in the simulation
    private float AverageRecentValues(float latestVal, float[] historyArray)
    {

        // Check if array is full
        if (!historyArray[historyArray.Length - 1].Equals(0))
        {
            // It is full, shift array left
            // Shift values left one index in array
            Array.Copy(historyArray, 1, historyArray, 0, historyArray.Length - 1);
            historyArray[historyArray.Length - 1] = 0;
        }

        // Use sum variable to add values in array
        float sum = 0;

        // Used for when we average values in array
        var lenNotNull = 0;

        // Add latest rotaion value into first null value. (This will  be right if it was full)
        for (int j = 0; j < historyArray.Length; j++)
        {
            // Sum array values that are not Null
            if (!historyArray[j].Equals(0))
            {
                // Sum values and keep track of length
                sum += historyArray[j];
                lenNotNull++;
            }
            else
            {
                // Insert latest values
                historyArray[j] = latestVal;
                sum += historyArray[j];
                lenNotNull++;
                break;
            }
        }

        // Average values
        return sum / lenNotNull;
    }

    // Function to log x-y coordinates of pen
    private static void LogCoordinates((float, float) UnityNormalizedCoordinates, float UnityRotationValue, TextWriter record)
    {
        // Print Link: Time xCoord yCoord RotationVal
        record.WriteLine(DateTime.Now.ToLongTimeString() + " " + UnityNormalizedCoordinates.Item1 + " " + UnityNormalizedCoordinates.Item2 + " " + UnityRotationValue);
    }

    // Function to update motor position
    private double CalculateMotorPosition(float yCoord)
    {
        // Default position is 0 if not in contact with surface
        double position = 0;

        // Check if ycoordinate is at or below Counter
        if (yCoord <= -4.3)
        {
            position = CalcMotorPositionBelowSurface(yCoord);
        }

        return position;
    }

    // Function to update motor position if hand is in contact with the surface
    // This scheme uses levels below the counter. Each level is separated by the same distance. When you reach a new level, the motor moves an additional set distance
    // Counter surface is at y = -4.3
    private double CalcMotorPositionBelowSurface(float ycoord)
    {
        var separationBetweenLevels = 0.5;
        var addedMotorDistBetweenLevels = 0.5; // in mm
        var motorPositionOnContact = 3; // in mm

        // Calculate number of levels the hand is below the counter
        var levelsBelowCounter = Math.Floor((Math.Abs(ycoord) - 4.3) / separationBetweenLevels);

        // Calculate position motor should be set to
        var motorPos = motorPositionOnContact + (levelsBelowCounter * addedMotorDistBetweenLevels);

        return motorPos;
    }

    // This function sends the motor position to the arduino over the UDP connection
    private void SendMotorPositionOverUDP()
    {
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        
        IPAddress broadcast = IPAddress.Parse(IP_address);

        byte[] sendbuf = Encoding.ASCII.GetBytes(string.Format("{0:N1}", motorPosition));
        IPEndPoint ep = new IPEndPoint(broadcast, 8080);

        s.SendTo(sendbuf, ep);
    }
}
