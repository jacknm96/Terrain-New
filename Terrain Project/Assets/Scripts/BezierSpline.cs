using UnityEngine;
using System;
using System.Collections.Generic;

public class BezierSpline : MonoBehaviour {

	[SerializeField]
	protected Vector3[] points;

	[SerializeField] protected BezierControlPointMode[] modes;
	[SerializeField] protected bool loop;

	List<Vector3> lut = new List<Vector3>();

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

	List<Vector3> GetLUT(int steps = 100)
	{
		if (lut.Count == steps)
		{
			return lut;
		}
		// n steps means n+1 points
		steps++;
		lut.Clear();
		for (int i = 0; i < steps; i++)
		{
			float t = (float)i / (steps - 1);
			Vector3 p = GetPoint(t);
			lut.Add(p);
		}
		return lut;
	}

	int FindClosest(List<Vector3> LUT, Vector3 point)
	{
		float mdist = float.MaxValue;
		int closestIndex = 0;
		for (int i = 0; i < LUT.Count; i++)
        {
			float d = Vector3.Distance(point, LUT[i]);
			if (d < mdist)
            {
				mdist = d;
				closestIndex = i;
            }
        }
		return closestIndex;
	}

	public Vector3 Project(Vector3 point)
	{
		// step 1: coarse check
		List<Vector3> LUT = GetLUT();
		int l = LUT.Count - 1;
		int closestIndex = FindClosest(LUT, point);
		float t1 = (float)(closestIndex - 1) / l;
		float t2 = (float)(closestIndex + 1) / l;
		float step = 0.1f / l;

		// step 2: fine check
		float mdist = Vector3.Distance(LUT[closestIndex], point);
		float d;
		Vector3 p;
		Vector3 closest = LUT[closestIndex];
		mdist += 1;
		for (float i = t1; i < t2 + step; i += step)
		{
			p = GetPoint(i);
			d = Vector3.Distance(point, p);
			if (d < mdist)
			{
				mdist = d;
				closest = p;
			}
		}
		return closest;
	}


}