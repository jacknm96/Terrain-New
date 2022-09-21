using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainPainter))]
public class TerrainPainterInspector : Editor
{
	private const int stepsPerCurve = 10;
	private const float directionScale = 0.5f;
	private const float handleSize = 0.04f;
	private const float pickSize = 0.06f;
	[SerializeField] bool showVelocity;

	//for handlemode settings
	private static Color[] modeColors = {
		Color.white,
		Color.yellow,
		Color.cyan
	};

	private TerrainPainter painter;
	private Transform handleTransform;
	private Quaternion handleRotation;

	//for selectable points
	private int selectedIndex = -1; //default value for none selected

	//when drawing the scene
	private void OnSceneGUI()
	{
		painter = target as TerrainPainter;
		handleTransform = painter.transform;
		handleRotation = Tools.pivotRotation == PivotRotation.Local ?
			handleTransform.rotation : Quaternion.identity;

		Vector3 p0 = ShowPoint(0);
		for (int i = 1; i < painter.GetPointCount; i += 3)
		{
			Vector3 p1 = ShowPoint(i);
			Vector3 p2 = ShowPoint(i + 1);
			Vector3 p3 = ShowPoint(i + 2);
			//draw the tangents
			Handles.color = Color.grey;
			Handles.DrawLine(p0, p1);
			Handles.DrawLine(p1, p2); //optional
			Handles.DrawLine(p2, p3);

			//for drawing the bezier curve in the editor			
			Handles.DrawBezier(p0, p3, p1, p2, Color.white, null, 2f);
			p0 = p3;
		}
		if (showVelocity)
		{
			ShowDirections();
		}

	}

	//for drawing the selected point
	private void DrawSelectedPointInspector()
	{
		GUILayout.Label("Selected Point");

		EditorGUI.BeginChangeCheck(); //set the position of the selected point
		Vector3 point = EditorGUILayout.Vector3Field("Position", painter.GetControlPoint(selectedIndex));

		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Move Point");
			EditorUtility.SetDirty(painter);
			painter.SetControlPoint(selectedIndex, point); //set the selected index
		}

		EditorGUI.BeginChangeCheck();  //set the mode of the selected point
		BezierControlPointMode mode = (BezierControlPointMode)EditorGUILayout.EnumPopup("Mode", painter.GetControlPointMode(selectedIndex));
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Change Point Mode");
			painter.SetControlPointMode(selectedIndex, mode);
			EditorUtility.SetDirty(painter);
		}
	}

	//for a custom Inspector
	public override void OnInspectorGUI()
	{
		painter = target as TerrainPainter;
		EditorGUI.BeginChangeCheck();

		//add an option to connect the first and last nodes 
		bool loop = EditorGUILayout.Toggle("Loop", painter.Loop);

		showVelocity = EditorGUILayout.Toggle("Show Velocity", showVelocity);


		if (EditorGUI.EndChangeCheck()) //returns true if editor changes
		{
			Undo.RecordObject(painter, "Toggle Loop");
			EditorUtility.SetDirty(painter);
			painter.Loop = loop;
		}
		if (selectedIndex >= 0 && selectedIndex < painter.GetPointCount)   //if default selected value of -1 we do not draw the point inspector
		{
			DrawSelectedPointInspector();
		}
		//add a button for adding a curve to the spline
		if (GUILayout.Button("Add Curve"))
		{
			Undo.RecordObject(painter, "Add Curve");
			painter.AddCurve();
			EditorUtility.SetDirty(painter);
		}

	}





	private void ShowDirections()
	{
		Handles.color = Color.green;
		Vector3 point = painter.GetPoint(0f);
		Handles.DrawLine(point, point + painter.GetVelocity(0f) * directionScale);
		int steps = stepsPerCurve * painter.CurveCount;
		for (int i = 1; i <= steps; i++)
		{
			point = painter.GetPoint(i / (float)steps);
			Handles.DrawLine(point, point + painter.GetVelocity(i / (float)steps) * directionScale);

		}
	}

	//only want to show the current selected point
	private Vector3 ShowPoint(int index)
	{
		Vector3 point = handleTransform.TransformPoint(painter.GetControlPoint(index));
		float size = HandleUtility.GetHandleSize(point);
		//make the first node bigger
		if (index == 0)
		{
			size *= 2f;
		}
		//set the color of handles
		Handles.color = modeColors[(int)painter.GetControlPointMode(index)];

		if (Handles.Button(point, handleRotation, size * handleSize, size * pickSize, Handles.DotHandleCap))
		{
			selectedIndex = index; //set the selected control point
			Repaint();
		}
		if (selectedIndex == index)
		{
			EditorGUI.BeginChangeCheck();
			point = Handles.DoPositionHandle(point, handleRotation);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(painter, "Move Point");
				EditorUtility.SetDirty(painter);
				painter.SetControlPoint(index, handleTransform.InverseTransformPoint(point));
			}
		}
		return point;
	}
}
