using UnityEngine;
using System;
using System.Collections.Generic;

public class BezierSpline : MonoBehaviour {

	[SerializeField]
	protected Vector3[] points;

	[SerializeField] protected BezierControlPointMode[] modes;
	[SerializeField] protected bool loop;

	//clear everything and reset to just a cubic bezier
	public void Reset()
	{
		points = new Vector3[] {
			new Vector3(1f, 0f, 0f),
			new Vector3(2f, 0f, 0f),
			new Vector3(3f, 0f, 0f),
			new Vector3(4f, 0f, 0f)
		};
		modes = new BezierControlPointMode[] {
			BezierControlPointMode.Free,
			BezierControlPointMode.Free
		};
	}

	public bool Loop {
		get {
			return loop;
		}
		set {
			loop = value;
			if (value == true) {
				modes[modes.Length - 1] = modes[0];
				SetControlPoint(0, points[0]);
			}
		}
	}

	public int GetPointCount {
		get {
			return points.Length;
		}
	}
	public int CurveCount {
		get {
			return (points.Length - 1) / 3;
		}
	}




	public Vector3 GetPoint (float t) {
		int i;
		if (t >= 1f) {
			t = 1f;
			i = points.Length - 4;
		}
		else {
			t = Mathf.Clamp01(t) * CurveCount;
			i = (int)t;
			t -= i;
			i *= 3;
		}
		//convert the local position of the point t in the bezier to world position
		return transform.TransformPoint(Bezier.GetPoint(points[i], points[i + 1], points[i + 2], points[i + 3], t));
	}
	
	public Vector3 GetVelocity (float t) {
		int i;
		if (t >= 1f) {
			t = 1f;
			i = points.Length - 4;
		}
		else {
			t = Mathf.Clamp01(t) * CurveCount;
			i = (int)t;
			t -= i;
			i *= 3;
		}
		return transform.TransformPoint(Bezier.GetFirstDerivative(points[i], points[i + 1], points[i + 2], points[i + 3], t)) - transform.position;
	}
	
	public Vector3 GetDirection (float t) {
		return GetVelocity(t).normalized;
	}

	public void AddCurve () {
		Vector3 point = points[points.Length - 1]; //remeber the current last node
		Array.Resize(ref points, points.Length + 3); //resize the array to contain another curve
		point.x += 1f;
		points[points.Length - 3] = point;
		point.x += 1f;
		points[points.Length - 2] = point;
		point.x += 1f;
		points[points.Length - 1] = point;

		Array.Resize(ref modes, modes.Length + 1);
		modes[modes.Length - 1] = modes[modes.Length - 2];
		EnforceMode(points.Length - 4);

		if (loop) {
			points[points.Length - 1] = points[0];//set the final node to the first node
			modes[modes.Length - 1] = modes[0]; //set the mode of the final node to the same as the first
			EnforceMode(0);
		}
	}

	public void RemoveCurve()
    {
		Vector3[] tempPoints = new Vector3[points.Length - 3];
		BezierControlPointMode[] tempModes = new BezierControlPointMode[modes.Length - 1];
		for (int i = 0; i < tempPoints.Length; i++)
        {
			tempPoints[i] = points[i];
			if (i < tempModes.Length) tempModes[i] = modes[i];
        }
		if (loop)
		{
			tempPoints[tempPoints.Length - 1] = tempPoints[0];//set the final node to the first node
			tempModes[tempModes.Length - 1] = tempModes[0]; //set the mode of the final node to the same as the first
			EnforceMode(0);
		}
		points = tempPoints;
		modes = tempModes;
	}

	public void SplitCurve(int num, int index)
	{
		if (num <= 1 || index < 0 || index >= points.Length) return;
		int offset = 3 * (num - 1);
		Vector3[] tempPoints = new Vector3[points.Length + offset]; // add space for new generated points
		BezierControlPointMode[] tempModes = new BezierControlPointMode[modes.Length + num - 1];
		int controlIndex = index - (index % 3);
		
		// copy un-split points
		for (int i = 0; i < controlIndex; i++)
        {
			tempPoints[i] = points[i];
			if (i < modes.Length) tempModes[i] = modes[i];
		}
		for (int i = controlIndex + 3; i < points.Length; i++)
		{
			tempPoints[i + offset] = points[i];
			if (i < modes.Length) tempModes[i + num - 1] = modes[i];
		}

		Vector3 p0 = points[controlIndex];
		float controlT = (float)(controlIndex / 3) / CurveCount;
		float stepSize = 1.0f / num / 3 / CurveCount;
		for (int i = 0; i < num; i++)
        {
			float u = controlT + stepSize * (3 * i + 1);
			float v = controlT + stepSize * (3 * i + 2);
			float w = controlT + stepSize * (3 * i + 3);
			Vector3 p1 = transform.InverseTransformPoint(GetPoint(u));
			Vector3 p2 = transform.InverseTransformPoint(GetPoint(v));
			Vector3 p3 = transform.InverseTransformPoint(GetPoint(w));
			Vector3 control1 = Vector3.zero, control2 = Vector3.zero;
			CalculateBezierControlPoints(p0, p1, p2, p3, 1 / 3.0f, 2 / 3.0f, ref control1, ref control2);
			int tempIndex = controlIndex + 3 * i;
			if (tempIndex >= tempPoints.Length - 2) break;
			tempPoints[tempIndex] = p0;
			tempPoints[tempIndex + 1] = control1;
			tempPoints[tempIndex + 2] = control2;
			tempPoints[tempIndex + 3] = p3;
			tempModes[(controlIndex / 3) + 1] = modes[controlIndex / 3];
			p0 = p3;
        }
		modes = tempModes;
		points = tempPoints;
	}



	public Vector3 GetControlPoint (int index) {
		return points[index];
	}

	//for setting the selected point
	public void SetControlPoint (int index, Vector3 point) {
		if (index % 3 == 0) {
			Vector3 delta = point - points[index];
			if (loop) {
				if (index == 0) {
					points[1] += delta;
					points[points.Length - 2] += delta;
					points[points.Length - 1] = point;
				}
				else if (index == points.Length - 1) {
					points[0] = point;
					points[1] += delta;
					points[index - 1] += delta;
				}
				else {
					points[index - 1] += delta;
					points[index + 1] += delta;
				}
			}
			else {
				if (index > 0) {
					points[index - 1] += delta;
				}
				if (index + 1 < points.Length) {
					points[index + 1] += delta;
				}
			}
		}
		points[index] = point;
		EnforceMode(index);
	}

	public bool CalculateBezierControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u, float v, ref Vector3 control1, ref Vector3 control2)
	{
		if ((u <= 0.0) || (u >= 1.0) || (v <= 0.0) || (v >= 1.0) || (u >= v))
			return false; /* failure */

		float a, b, c, d, det;
		a = 3 * (1 - u) * (1 - u) * u;
		b = 3 * (1 - u) * u * u;
		c = 3 * (1 - v) * (1 - v) * v;
		d = 3 * (1 - v) * v * v;
		det = a * d - b * c;
		/* unnecessary, but just in case... */
		if (det == 0.0) return false; /* failure */

		Vector3 q1 = p1 - new Vector3(((1 - u) * (1 - u) * (1 - u) * p0.x + u * u * u * p3.x),
									((1 - u) * (1 - u) * (1 - u) * p0.y + u * u * u * p3.y),
									((1 - u) * (1 - u) * (1 - u) * p0.z + u * u * u * p3.z));
		Vector3 q2 = p2 - new Vector3(((1 - v) * (1 - v) * (1 - v) * p0.x + v * v * v * p3.x),
									((1 - v) * (1 - v) * (1 - v) * p0.y + v * v * v * p3.y),
									((1 - v) * (1 - v) * (1 - v) * p0.z + v * v * v * p3.z));

		control1.x = d * q1.x - b * q2.x;
		control1.y = d * q1.y - b * q2.y;
		control1.z = d * q1.z - b * q2.z;
		control1 /= det;

		control2.x = (-c) * q1.x + a * q2.x;
		control2.y = (-c) * q1.y + a * q2.y;
		control2.z = (-c) * q1.z + a * q2.z;
		control2 /= det;

		return true; /* success */
	}

	public BezierControlPointMode GetControlPointMode (int index) {
		return modes[(index + 1) / 3];  
	}

	public void SetControlPointMode (int index, BezierControlPointMode mode) {
		int modeIndex = (index + 1) / 3;
		modes[modeIndex] = mode;
		if (loop) {
			if (modeIndex == 0) {
				modes[modes.Length - 1] = mode;
			}
			else if (modeIndex == modes.Length - 1) {
				modes[0] = mode;
			}
		}
		EnforceMode(index);
	}

	private void EnforceMode (int index) {
		int modeIndex = (index + 1) / 3;
		BezierControlPointMode mode = modes[modeIndex];
		//if node mode is free or if not looping and not the first and last node
		if (mode == BezierControlPointMode.Free || !loop && (modeIndex == 0 || modeIndex == modes.Length - 1)) {
			return;
		}
		//for all other cases
		int middleIndex = modeIndex * 3; //the middle node
		int fixedIndex, enforcedIndex;
		if (index <= middleIndex) { 
			fixedIndex = middleIndex - 1; //the lesser node
			if (fixedIndex < 0) {
				fixedIndex = points.Length - 2;
			}
			enforcedIndex = middleIndex + 1; //the greater node
			if (enforcedIndex >= points.Length) {
				enforcedIndex = 1;
			}
		}
		else {
			fixedIndex = middleIndex + 1; //the greater node
			if (fixedIndex >= points.Length) {
				fixedIndex = 1;
			}
			enforcedIndex = middleIndex - 1; //the lesser node
			if (enforcedIndex < 0) {
				enforcedIndex = points.Length - 2;
			}
		}
		//get the position of the middle node as a reference position for where to position after enforcing
		Vector3 middle = points[middleIndex];
		Vector3 enforcedTangent = middle - points[fixedIndex];
		if (mode == BezierControlPointMode.Aligned) {
			enforcedTangent = enforcedTangent.normalized * Vector3.Distance(middle, points[enforcedIndex]);
		}
		points[enforcedIndex] = middle + enforcedTangent;
	}


}