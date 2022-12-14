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
	//[SerializeField] Texture2D brush;
	SerializedObject so;
	SerializedProperty brush;
	int brushSize;
	float brushStrength;
	bool terrainPaint;
	bool alignHeight;
	bool foldoutPaintingSettings = true;
	bool foldoutHeightSettings = true;
	bool painting;
	int stepSizePerCurve;
	int layerPaint;

	int heightArea;
	int pathArea;
	float heightSlope;
	float smoothStrength;
	AnimationCurve slopeCurve;
	bool useSlopeCurve;

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
	private int curvesToSplitInto;
	private bool decidingSplit;

	//grab our serialized properties
    private void OnEnable()
    {
		so = serializedObject;
		brush = so.FindProperty(nameof(TerrainPainter.brushIMG));
		Undo.undoRedoPerformed += OnUndo;
    }

    //when drawing the scene
    private void OnSceneGUI()
	{
		painter = target as TerrainPainter;
		handleTransform = painter.transform;
		handleRotation = Tools.pivotRotation == PivotRotation.Local ?
			handleTransform.rotation : Quaternion.identity;

		//Vector3 p0 = ShowPoint(0);
		for (int i = 1; i < painter.GetPointCount; i += 3)
		{
            /*Vector3 p1 = ShowPoint(i);
            Vector3 p2 = ShowPoint(i + 1);
            Vector3 p3 = ShowPoint(i + 2);
            //draw the tangents
            Handles.color = Color.grey;
            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p1, p2); //optional
            Handles.DrawLine(p2, p3);

            //for drawing the bezier curve in the editor			
            Handles.DrawBezier(p0, p3, p1, p2, Color.white, null, 2f);
            p0 = p3;*/

            Vector3[] controlPoints = ShowPoints(i - 1);
            Handles.color = Color.grey;
            Handles.DrawLine(controlPoints[0], controlPoints[1]);
            Handles.DrawLine(controlPoints[1], controlPoints[2]); //optional
            Handles.DrawLine(controlPoints[2], controlPoints[3]);
            Handles.DrawBezier(controlPoints[0], controlPoints[3], controlPoints[1], controlPoints[2], Color.white, null, 2f);
        }

		//draws where along the curve brush will paint, and radius of brush
		if ((!painting) && (terrainPaint || alignHeight))
        {
			Vector3 point;
			int steps = stepSizePerCurve * painter.CurveCount;
			if (steps > 0)
            {
				for (int i = 0; i <= steps; i++)
				{
					point = painter.GetPoint(i / (float)steps);
					if (terrainPaint)
                    {
						Handles.color = Color.white;
						Handles.DrawWireDisc(point, Vector3.up, brushSize);
					}
					if (alignHeight)
                    {
						Handles.color = Color.red;
						Handles.DrawWireDisc(point, Vector3.up, heightArea);
						Handles.color = Color.blue;
						Handles.DrawWireDisc(point, Vector3.up, pathArea);
					}
				}
			}
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

		EditorGUI.BeginChangeCheck();
		//set the position of the selected point
		int mod = selectedIndex % 3;
		int index = selectedIndex - mod;
		Vector3[] controlPoints = new Vector3[4];
		for (int i = index; i < index + 4; i++)
		{
            try { controlPoints[i - index] = handleTransform.TransformPoint(painter.GetControlPoint(i)); }
            catch (System.Exception) // for if the selected index is the final point
            {
                //Debug.Log(i - index);
            }
        }
		Vector3 handlePoint1 = painter.GetPoint((index + 1) / (float)(painter.CurveCount * 3));
		Vector3 handlePoint2 = painter.GetPoint((index + 2) / (float)(painter.CurveCount * 3));
		Vector3[] handlePoints = { controlPoints[0], handlePoint1, handlePoint2, controlPoints[3] }; // points that are shown to use, along curve
		Vector3 point = EditorGUILayout.Vector3Field("Position", handlePoints[selectedIndex - index]);

		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Move Point");
			if (mod != 0)
            {
                handlePoints[mod] = point;
                if (painter.CalculateBezierControlPoints(controlPoints[0], handlePoints[1], handlePoints[2], controlPoints[3], 1 / 3.0f, 2 / 3.0f, ref controlPoints[1], ref controlPoints[2]))
                {
                    painter.SetControlPoint(index + 1, handleTransform.InverseTransformPoint(controlPoints[1]));
                    painter.SetControlPoint(index + 2, handleTransform.InverseTransformPoint(controlPoints[2]));
                }
            }
            else painter.SetControlPoint(selectedIndex, handleTransform.InverseTransformPoint(point)); //set the selected index
            //painter.SetControlPoint(selectedIndex, point); //set the selected index
            if (painting) // if painting, realign paints with modified bezier curve
			{
				painter.UndoPaint();
				painter.PaintAlongProjectedBezier();
			}
			EditorUtility.SetDirty(painter);
		}

		EditorGUI.BeginChangeCheck();  //set the mode of the selected point
		BezierControlPointMode mode = (BezierControlPointMode)EditorGUILayout.EnumPopup("Mode", painter.GetControlPointMode(selectedIndex));
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Change Point Mode");
			painter.SetControlPointMode(selectedIndex, mode);
			EditorUtility.SetDirty(painter);
		}

		if (decidingSplit)
        {
			EditorGUILayout.BeginHorizontal();

			EditorGUI.BeginChangeCheck();
			curvesToSplitInto = EditorGUILayout.IntField("Curves To Split", painter.curvesToSplitInto);
			if (EditorGUI.EndChangeCheck()) //returns true if editor changes
			{
				Undo.RecordObject(painter, "Curves To Split");
				curvesToSplitInto = Mathf.Max(curvesToSplitInto, 2);
				painter.curvesToSplitInto = curvesToSplitInto;
				EditorUtility.SetDirty(painter);
			}

			if (GUILayout.Button("Split"))
			{
				Undo.RecordObject(painter, "Split Curve");
				painter.SplitCurve(painter.curvesToSplitInto, selectedIndex);
				decidingSplit = false;
				if (painting)
				{
					painter.UndoPaint();
					painter.PaintAlongProjectedBezier();
				}
				EditorUtility.SetDirty(painter);
			}

			if (GUILayout.Button("Cancel"))
			{
				Undo.RecordObject(painter, "Cancel Split");
				decidingSplit = false;
				EditorUtility.SetDirty(painter);
			}

			EditorGUILayout.EndHorizontal();
        }
		//add a button for splitting the curve
		else if (GUILayout.Button("Split Curve"))
		{
			Undo.RecordObject(painter, "Decide Split");
			decidingSplit = true;
			EditorUtility.SetDirty(painter);
		}
	}

	//for a custom Inspector
	public override void OnInspectorGUI()
	{
		painter = target as TerrainPainter;
		EditorGUILayout.LabelField("Painting Info", EditorStyles.boldLabel);

		// set steps per curve
		EditorGUI.BeginChangeCheck();
		stepSizePerCurve = EditorGUILayout.IntField(new GUIContent("Steps Per Curve", "Iterations per curve the painter will cast to the terrain"), painter.stepsPerCurve); ;
		if (EditorGUI.EndChangeCheck()) //returns true if editor changes
		{
			Undo.RecordObject(painter, "Change Steps Per Curve");
			stepSizePerCurve = Mathf.Max(stepSizePerCurve, 1);
			painter.stepsPerCurve = stepSizePerCurve;
			if (painting)
			{
				painter.UndoPaint();
				painter.PaintAlongProjectedBezier();
			}
			EditorUtility.SetDirty(painter);
		}

		// align height toggle
		EditorGUI.BeginChangeCheck();
		terrainPaint = EditorGUILayout.Toggle("Paint Texture", painter.paintTerrain);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Paint toggle");
			painter.paintTerrain = terrainPaint;
			if (painting)
			{
				if (terrainPaint) painter.StartTerrainPaint();
				else painter.UndoTerrainPaint();
				painter.UndoPaint();
				painter.PaintAlongProjectedBezier();
			}
			EditorUtility.SetDirty(painter);
		}
		if (terrainPaint)
        {
			foldoutPaintingSettings = EditorGUILayout.Foldout(foldoutPaintingSettings, "Paintbrush Settings");
			if (foldoutPaintingSettings)
			{
				EditorGUI.indentLevel++;
				// set brush size
				EditorGUI.BeginChangeCheck();
				brushSize = EditorGUILayout.IntField("Brush Size", painter.areaOfEffectSize);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Brush Size");
					brushSize = Mathf.Max(brushSize, 2);
					painter.areaOfEffectSize = brushSize;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set brush strength
				EditorGUI.BeginChangeCheck();
				brushStrength = EditorGUILayout.Slider(new GUIContent("Brush Strength", "Opacity"), painter.paintStrength, 0.0f, 1.0f);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Brush Strength");
					brushStrength = Mathf.Clamp01(brushStrength);
					painter.paintStrength = brushStrength;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set brush img
				EditorGUI.BeginChangeCheck();
				//brush.objectReferenceValue = EditorGUILayout.ObjectField("Brush", brush.objectReferenceValue, typeof(Texture2D), false) as Texture2D;
				EditorGUILayout.PropertyField(brush, new GUIContent("Brush Image", "Shape of the paint brush"));
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Brush Texture");
					painter.brushIMG = (Texture2D)brush.objectReferenceValue;
					//painter.SetBrush();
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set paint texture
				EditorGUI.BeginChangeCheck();
				layerPaint = EditorGUILayout.IntField(new GUIContent("Paint Layer", "Index of the layer paint to use from the terrain component"), painter.paint);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Paint Layer");
					layerPaint = Mathf.Max(layerPaint, 0);
					painter.paint = layerPaint;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}
				EditorGUI.indentLevel--;
			}
		}

		EditorGUILayout.Space();

		// align height toggle
		EditorGUI.BeginChangeCheck();
		alignHeight = EditorGUILayout.Toggle("Snap Height", painter.paintHeight);
		if (EditorGUI.EndChangeCheck())
        {
			Undo.RecordObject(painter, "Height toggle");
			painter.paintHeight = alignHeight;
			if (painting)
			{
				if (alignHeight) painter.StartHeightAdjustment();
				else painter.UndoHeightAdjustment();
				painter.UndoPaint();
				painter.PaintAlongProjectedBezier();
			}
			EditorUtility.SetDirty(painter);
        }
		if (alignHeight)
		{
			foldoutHeightSettings = EditorGUILayout.Foldout(foldoutHeightSettings, "Height Alignment Settings");
			if (foldoutHeightSettings)
			{
				EditorGUI.indentLevel++;

				// set height adjustment area
				EditorGUI.BeginChangeCheck();
				heightArea = EditorGUILayout.IntField(new GUIContent("Brush Size", "Area to be adjusted by height alignment"), painter.heightAdjustmentArea);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Height Area");
					heightArea = Mathf.Max(heightArea, 1);
					painter.heightAdjustmentArea = heightArea;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set path adjustment area
				EditorGUI.BeginChangeCheck();
				pathArea = EditorGUILayout.IntSlider(new GUIContent("Path Area", "Flat area for path"), painter.heightPathArea, 0, painter.heightAdjustmentArea);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Path Area");
					pathArea = Mathf.Max(pathArea, 0);
					painter.heightPathArea = pathArea;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set height adjustment slope
				EditorGUI.BeginChangeCheck();
				heightSlope = EditorGUILayout.Slider(new GUIContent("Brush Slope", "Value of 0 will be completely flat, 1 is a sheer cliff"), painter.heightAdjustmentSlope, 0.0f, 1.0f);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Height Slope");
					heightSlope = Mathf.Clamp01(heightSlope);
					painter.heightAdjustmentSlope = heightSlope;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// set height smoothing strength
				EditorGUI.BeginChangeCheck();
				smoothStrength = EditorGUILayout.Slider(new GUIContent("Smoothing", "Value of 0 will have no smoothing"), painter.smoothStrength, 0.0f, 1.0f);
				if (EditorGUI.EndChangeCheck()) //returns true if editor changes
				{
					Undo.RecordObject(painter, "Change Smoothing");
					smoothStrength = Mathf.Clamp01(smoothStrength);
					painter.smoothStrength = smoothStrength;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}

				// use slope curve toggle
				EditorGUI.BeginChangeCheck();
				useSlopeCurve = EditorGUILayout.Toggle("Use Slope Curve", painter.useSlopeCurve);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(painter, "Use Slope toggle");
					painter.useSlopeCurve = useSlopeCurve;
					if (painting)
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}
				if (useSlopeCurve)
                {
					EditorGUI.BeginChangeCheck();
					slopeCurve = EditorGUILayout.CurveField("Use Slope Curve", painter.slopeCurve);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(painter, "Change Slope Curve");
						painter.slopeCurve = slopeCurve;
						if (painting)
						{
							painter.UndoPaint();
							painter.PaintAlongProjectedBezier();
						}
						EditorUtility.SetDirty(painter);
					}
				}

				EditorGUI.indentLevel--;
			}
			if (!Selection.activeTransform)
			{
				foldoutHeightSettings = false;
			}
		}

		EditorGUILayout.Space();

		// start painting
		EditorGUI.BeginChangeCheck();
		painting = EditorGUILayout.Toggle("Start Painting", painter.painting);
		if (EditorGUI.EndChangeCheck())
		{
			Undo.RecordObject(painter, "Start/Stop Painting");
			if (painting)
			{
				painter.LinkTerrain();
				painter.StartPainting(); // cache paint values when start painting for reverting
				painter.GenerateBrush(painter.brushIMG, painter.areaOfEffectSize);
				painter.PaintAlongProjectedBezier();
			}
			else
			{
				painter.UndoPaint();
				painter.painting = painting = false;
			}
			EditorUtility.SetDirty(painter);
		}
		if (painting)
        {
			//reset button
			if (GUILayout.Button("Revert Changes"))
			{
				Undo.RecordObject(painter, "Reverting Changes");
				painter.UndoPaint();
				painter.painting = painting = false;
				EditorUtility.SetDirty(painter);
			}
			//bake changes
			if (GUILayout.Button("Bake"))
			{
				Undo.RecordObject(painter, "Bake Paints");
				painter.Bake();
				painter.painting = painting = false;
				EditorUtility.SetDirty(painter);
			}
		}
		

		EditorGUILayout.Space();


		EditorGUILayout.LabelField("Bezier Info", EditorStyles.boldLabel);

		EditorGUI.BeginChangeCheck();

		//add an option to connect the first and last nodes 
		bool loop = EditorGUILayout.Toggle("Loop", painter.Loop);

		showVelocity = EditorGUILayout.Toggle("Show Velocity", showVelocity);


		if (EditorGUI.EndChangeCheck()) //returns true if editor changes
		{
			Undo.RecordObject(painter, "Toggle Loop");
			painter.Loop = loop;
			EditorUtility.SetDirty(painter);
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
			if (painting)
			{
				painter.UndoPaint();
				painter.PaintAlongProjectedBezier();
			}
			EditorUtility.SetDirty(painter);
		}
		if (painter.CurveCount > 1)
        {
			//add a button for removing a curve from the spline
			if (GUILayout.Button("Remove Curve"))
			{
				Undo.RecordObject(painter, "Remove Curve");
				painter.RemoveCurve();
				if (painting)
                {
					painter.UndoPaint();
					painter.PaintAlongProjectedBezier();
				}
				EditorUtility.SetDirty(painter);
			}
		}

	}

	// draws the tangent vectors along the bezier curve using handles
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
				if (painting) // if painting, realign paints with modified bezier curve
                {
					painter.UndoPaint();
					painter.PaintAlongProjectedBezier();
                }
			}
		}
		return point;
	}

	Vector3[] ShowPoints(int index)
    {
		Vector3[] controlPoints = new Vector3[4];
		for (int i = index; i < index + 4; i++)
        {
			controlPoints[i - index] = handleTransform.TransformPoint(painter.GetControlPoint(i));
			//controlPoints[i - index] = painter.GetControlPoint(i);
		}
		Vector3 handlePoint1 = painter.GetPoint((index + 1) / (float)(painter.CurveCount * 3));
		Vector3 handlePoint2 = painter.GetPoint((index + 2) / (float)(painter.CurveCount * 3));
		Vector3[] handlePoints = {controlPoints[0], handlePoint1, handlePoint2, controlPoints[3] }; // points that are shown to use, along curve
		for (int i = 0; i < handlePoints.Length; i++)
        {
			float size = HandleUtility.GetHandleSize(handlePoints[i]);
			//make the first node bigger
			if (index + i == 0)
			{
				size *= 2f;
			}
			//set the color of handles
			Handles.color = modeColors[(int)painter.GetControlPointMode(index)];

			if (Handles.Button(handlePoints[i], handleRotation, size * handleSize, size * pickSize, Handles.DotHandleCap))
			{
				selectedIndex = index + i; //set the selected control point
				Repaint();
			}
			if (selectedIndex == index + i)
			{
				EditorGUI.BeginChangeCheck();
				handlePoints[i] = Handles.DoPositionHandle(handlePoints[i], handleRotation);
				//if (i == index + 1 || i == index + 2) handlePoints[i] = handleTransform.InverseTransformPoint(handlePoints[i]);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(painter, "Move Point");
					//painter.SetControlPoint(index, handleTransform.InverseTransformPoint(handlePoints[i]));

					// if index % 3 == 0, we are at root point and no difference between handle and control point
					if (selectedIndex % 3 != 0 && painter.CalculateBezierControlPoints(handlePoints[0], handlePoints[1], handlePoints[2], handlePoints[3], 1 / 3.0f, 2 / 3.0f, ref controlPoints[1], ref controlPoints[2]))
					{
						painter.SetControlPoint(index + 1, handleTransform.InverseTransformPoint(controlPoints[1]));
						painter.SetControlPoint(index + 2, handleTransform.InverseTransformPoint(controlPoints[2]));
					} else painter.SetControlPoint(index + i, handleTransform.InverseTransformPoint(handlePoints[i]));
					if (painting) // if painting, realign paints with modified bezier curve
					{
						painter.UndoPaint();
						painter.PaintAlongProjectedBezier();
					}
					EditorUtility.SetDirty(painter);
				}
			}
		}
		return controlPoints;
    }

	void OnUndo()
	{
		if (painter != null)
		{
			painter.UndoPaint();
			if (painter.painting) painter.PaintAlongProjectedBezier();
		}
	}
}
