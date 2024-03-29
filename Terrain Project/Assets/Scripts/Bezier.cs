﻿using UnityEngine;

public static class Bezier {

	//quadratic
	public static Vector3 GetPoint (Vector3 p0, Vector3 p1, Vector3 p2, float t) {
		if (t <= 0)
		{
			return p0;
		}
		else if (t >= 1)
		{
			return p2;
		}
		else
		{
			return
			(1 - t) * (1 - t) * p0 +
			2 * (1 - t) * t * p1 +
			t * t * p2;
		}
	}


	//quadratic
	public static Vector3 GetFirstDerivative (Vector3 p0, Vector3 p1, Vector3 p2, float t) {
		return 
			2f * (1f - t) * (p1 - p0) +
			2f * t * (p2 - p1);
	}

	//cubic
	public static Vector3 GetPoint (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
		if (t <= 0)
		{
			return p0;
		}
		else if (t >= 1)
		{
			return p3;
		}
		else
		{
			return
			(1f - t) * (1f - t) * (1f - t) * p0 +
			3f * (1f - t) * (1f - t) * t * p1 +
			3f * (1f - t) * t * t * p2 + 
			t * t * t * p3;
		}
		
	}
	// velocity of cubic bezier at point t
	public static Vector3 GetFirstDerivative (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
		t = Mathf.Clamp01(t);
		return
			3f * (1f - t) * (1f - t) * (p1 - p0) +
			6f * (1f - t) * t * (p2 - p1) +
			3f * t * t * (p3 - p2);
	}
}