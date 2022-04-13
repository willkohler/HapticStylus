using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using OpenCvSharp.Util;
using OpenCvSharp.Aruco;

public class Tracking : MonoBehaviour
{
    // Start is called before the first frame update
    public Texture2D myTexture;
	public WebCamTexture myWebCamTexture;

	public OpenCvSharp.Point2f[][] corners;

	void Start () {

		WebCamDevice[] devices = WebCamTexture.devices;
		//myWebCamTexture = new WebCamTexture(devices[1].name, 1920, 1080, 60);
		myWebCamTexture = new WebCamTexture(devices[0].name, 1920, 1080, 60);
		myWebCamTexture.Play();
		//GetComponent<Renderer>().material.mainTexture = myWebCamTexture;
    }

    // Update is called once per frame
    void Update()
    {
        DetectorParameters detectorParameters = DetectorParameters.Create();

	    // Dictionary holds set of all available markers
		Dictionary dictionary = CvAruco.GetPredefinedDictionary (PredefinedDictionaryName.Dict6X6_250);

		// Variables to hold results
		int[] ids;
        OpenCvSharp.Point2f[][] rejectedImgPoints;

        Mat frame = OpenCvSharp.Unity.TextureToMat (myWebCamTexture);

		// Convert image to grasyscale
		Mat grayFrame = new Mat ();
		Cv2.CvtColor (frame, grayFrame, ColorConversionCodes.BGR2GRAY); 
		// Detect and draw markers
		CvAruco.DetectMarkers (grayFrame, dictionary, out corners, out ids, detectorParameters, out rejectedImgPoints);
    }
}
