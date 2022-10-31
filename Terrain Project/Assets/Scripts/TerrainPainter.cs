using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainPainter : BezierSpline
{
    #region Painting
    public Terrain terrain;
    TerrainData currentTerrainData;

    float[,] terrainHeightMap;  //a 2d array of floats to store 
    int terrainHeightMapWidth; //Used to calculate click position
    int terrainHeightMapHeight;

    float[,] heights; //a variable to store the new heights
    float[,] undoHeightHolder = new float[0, 0]; // holds height map information from when we started editing for the purpose of reverting changes
    TerrainData targetTerrainData; // stores the terrains terrain data
    public enum EffectType
    {
        raise,
        lower,
        flatten,
        smooth,
        paint,
        heightSnap
    };
    public Texture2D brushIMG; // This will allow you to switch brushes
    float[,] paintBrush; // this stores the brush.png pixel data
    float[,] heightBrush; // this stores the brush.png pixel data
    public int areaOfEffectSize = 100; // size of the brush
    [Range(0.01f, 1f)] // you can remove this if you want
    public float paintStrength; // brush strength
    public float smoothStrength; // height strength
    public float flattenHeight = 0; // the height to which the flatten mode will go
    public EffectType effectType;
    public TerrainLayer paints;// a list containing all of the paints
    public int paint; // variable to select paint
    float[,,] splat; // A splat map is what unity uses to overlay all of your paints on to the terrain
    float[,,] undoSplatHolder = new float[0, 0, 0]; // holds splat map information from when we started editing for the purpose of reverting changes
    public int stepsPerCurve;
    public bool terrainPaint;
    public bool snapHeight;
    public bool painting;

    public int heightAdjustmentArea;
    public float heightAdjustmentSlope;

    Vector3 startPos;
    Vector3 endPos;
    
    // Start is called before the first frame update
    void Start()
    {
        GenerateBrush(brushIMG, areaOfEffectSize);
        GenerateBrush(brushIMG, heightAdjustmentArea, true);
        effectType = EffectType.paint;
        terrain = FindObjectOfType<Terrain>();
        currentTerrainData = terrain.terrainData;
    }

    // Update is called once per frame
    void Update()
    {
        /*if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                terrain = GetTerrainAtObject(hit.transform.gameObject);
                SetEditValues(terrain);
                GetTerrainCoordinates(hit, out int terX, out int terZ);
                terX = (int)Mathf.Max(0, terX - areaOfEffectSize / 2);
                terZ = (int)Mathf.Max(0, terZ - areaOfEffectSize / 2);
                effectType = EffectType.paint;
                paint = 1;
                ModifyTerrain(terX, terZ);
            }
        }*/
    }

    // https://answers.unity.com/questions/1743422/paint-terrain-texture-on-runtime.html

    public Terrain GetTerrainAtObject(GameObject gameObject)
    {
        if (gameObject.GetComponent<Terrain>())
        {
            //This will return the Terrain component of an object (if present)
            return gameObject.GetComponent<Terrain>();
        }
        return default(Terrain);
    }

    /// <summary>
    /// Set some of our variables
    /// </summary>
    /// <param name="terrain"></param>
    public void SetEditValues(Terrain terrain)
    {
        currentTerrainData = GetCurrentTerrainData();
        terrainHeightMap = GetCurrentTerrainHeightMap();
        terrainHeightMapWidth = GetCurrentTerrainWidth();
        terrainHeightMapHeight = GetCurrentTerrainHeight();
    }

    /// <summary>
    /// Takes in a RaycastHit on the terrain object, and returns the corresponding terrain coordinates
    /// </summary>
    /// <param name="hit"></param>
    /// <param name="x"></param>
    /// <param name="z"></param>
    public void GetTerrainCoordinates(RaycastHit hit, out int x, out int z)
    {
        Vector3 tempTerrainCoodinates = hit.point - hit.transform.position;
        //This takes the world coords and makes them relative to the terrain
        Vector3 terrainCoordinates = new Vector3(
            tempTerrainCoodinates.x / GetTerrainSize().x,
            tempTerrainCoodinates.y / GetTerrainSize().y,
            tempTerrainCoodinates.z / GetTerrainSize().z);
        // This will take the coords relative to the terrain and make them relative to the height map(which often has different dimensions)
        Vector3 locationInTerrain = new Vector3
            (
            terrainCoordinates.x * terrainHeightMapWidth,
            0,
            terrainCoordinates.z * terrainHeightMapHeight
            );
        //Finally, this will spit out the X Y values for use in other parts of the code
        x = (int)locationInTerrain.x;
        z = (int)locationInTerrain.z;
    }

    /// <summary>
    /// Used for smoothing, and returns the average height of the surrounding points
    /// </summary>
    /// <param name="height"></param>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    private float GetSurroundingHeights(float[,] height, int x, int z)
    {
        float value; // this will temporarily hold the value at each point
        float avg = terrainHeightMap[x, z]; // we will add all the heights to this and divide by int num bellow to get the average height
        int num = 1;
        for (int i = 0; i < 4; i++) //this will loop us through the possible surrounding spots
        {
            try // This will try to run the code bellow, and if one of the coords is not on the terrain(ie we are at an edge) it will pass the exception to the Catch{} below
            {
                // These give us the values surrounding the point
                if (i == 0)
                { value = terrainHeightMap[x + 1, z]; }
                else if (i == 1)
                { value = terrainHeightMap[x - 1, z]; }
                else if (i == 2)
                { value = terrainHeightMap[x, z + 1]; }
                else
                { value = terrainHeightMap[x, z - 1]; }
                num++; // keeps track of how many iterations were successful  
                avg += value;
            }
            catch (System.Exception)
            {
            }
        }
        avg = avg / num;
        return avg;
    }

    /// <summary>
    /// Size of the terrain in 3 dimensions
    /// </summary>
    /// <returns></returns>
    public Vector3 GetTerrainSize()
    {
        return currentTerrainData.size;
    }

    /// <summary>
    /// Passes the terrain data of the current terrain
    /// </summary>
    /// <returns></returns>
    public TerrainData GetCurrentTerrainData()
    {
        if (terrain)
        {
            return terrain.terrainData;
        }
        return default(TerrainData);
    }
    /// <summary>
    /// Returns the height map of the terrain
    /// </summary>
    /// <returns></returns>
    public float[,] GetCurrentTerrainHeightMap()
    {
        if (terrain)
        {
            // the first 2 0's indicate the coords where we start, the next values indicate how far we extend the area,
            // so what we are saying here is I want the heights starting at the Origin and extending the entire width and height of the terrain
            return terrain.terrainData.GetHeights(0, 0,
            terrain.terrainData.heightmapResolution,
            terrain.terrainData.heightmapResolution);
        }
        return default(float[,]);
    }

    /// <summary>
    /// Returns width of the terrain
    /// </summary>
    /// <returns></returns>
    public int GetCurrentTerrainWidth()
    {
        if (terrain)
        {
            return currentTerrainData.heightmapResolution;
        }
        return 0;
    }

    /// <summary>
    /// Returns height of the terrain
    /// </summary>
    /// <returns></returns>
    public int GetCurrentTerrainHeight()
    {
        if (terrain)
        {
            return currentTerrainData.heightmapResolution;
        }
        return 0;
        //test2.GetComponent<MeshRenderer>().material.mainTexture = texture;
    }

    /// <summary>
    /// Converts the y value from local space (difference between value and terrain's world y) into terrain height from 0 to 1
    /// </summary>
    /// <param name="yVal"></param>
    /// <returns></returns>
    public float GetTerrainHeight(float yVal)
    {
        var heightScale = 1.0f / currentTerrainData.size.y;
        return yVal * heightScale;
    }

    /// <summary>
    /// Generates a brush for use of painting the terrain
    /// </summary>
    /// <param name="texture"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public float[,] GenerateBrush(Texture2D texture, int size, bool height = false)
    {
        float[,] heightMap = new float[size, size];//creates a 2d array which will store our brush
        Texture2D scaledBrush = ResizeBrush(texture, size, size); 
        //This will iterate over the entire re-scaled image and convert the pixel color into a value between 0 and 1
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Color pixelValue = scaledBrush.GetPixel(x, y);
                heightMap[x, y] = pixelValue.grayscale / 255;
            }
        }
        if (!height) paintBrush = heightMap;
        else heightBrush = heightMap;
        return heightMap;
    }

    /// <summary>
    /// Resizes the brush
    /// </summary>
    /// <param name="src"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static Texture2D ResizeBrush(Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear)
    {
        Rect texR = new Rect(0, 0, width, height);
        _gpu_scale(src, width, height, mode);
        //Get rendered data back to a new texture
        Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, true);
        result.Resize(width, height);
        result.ReadPixels(texR, 0, 0, true);
        return result;
    }

    static void _gpu_scale(Texture2D src, int width, int height, FilterMode fmode)
    {
        //We need the source texture in VRAM because we render with it
        src.filterMode = fmode;
        src.Apply(true);
        //Using RTT for best quality and performance. Thanks, Unity 5
        RenderTexture rtt = new RenderTexture(width, height, 32);
        //Set the RTT in order to render to it
        Graphics.SetRenderTarget(rtt);
        //Setup 2D matrix in range 0..1, so nobody needs to care about sized
        GL.LoadPixelMatrix(0, 1, 1, 0);
        //Then clear & draw the texture to fill the entire RTT.
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new Rect(0, 0, 1, 1), src);
    }

    public void SetPaint(int num)
    {
        paint = num;
    }
    public void SetLayers(TerrainData t)
    {
        //t.terrainLayers = paints;
    }
    public void SetBrushSize(int value)//adds int value to brush size(make negative to shrink)
    {
        areaOfEffectSize += value;
        if (areaOfEffectSize > 50)
        { areaOfEffectSize = 50; }
        else if (areaOfEffectSize < 1)
        { areaOfEffectSize = 1; }
        paintBrush = GenerateBrush(brushIMG, areaOfEffectSize); // regenerates the brush with new size
    }
    public void SetBrushStrength(float value)//same idea as SetBrushSize()
    {
        paintStrength += value;
        if (paintStrength > 1)
        { paintStrength = 1; }
        else if (paintStrength < 0.01f)
        { paintStrength = 0.01f; }
    }
    public void SetBrush(bool height = false)
    {
        GenerateBrush(brushIMG, height ? heightAdjustmentArea : areaOfEffectSize, height);
        //RMC.SetIndicators();
    }

    public void SetHeights()
    {
        heights = GetCurrentTerrainHeightMap();
    }

    public void ModifyTerrain(int x, int z, float y = 0)
    {
        //These AreaOfEffectModifier variables below will help us if we are modifying terrain that goes over the edge,
        //you will see in a bit that I use Xmod for the the z(or Y) values, which was because I did not realize at first
        //that the terrain X and world X is not the same so I had to flip them around and was too lazy to correct the names, so don't get thrown off by that.
        int AOExMod = 0;
        int AOEzMod = 0;
        int AOExMod1 = 0;
        int AOEzMod1 = 0;

        int areaSize = (effectType == EffectType.paint) ? areaOfEffectSize : heightAdjustmentArea;

        if (x < 0) // if the brush goes off the negative end of the x axis we set the mod == to it to offset the edited area
        {
            AOExMod = x;
        }
        else if (x + areaSize > terrainHeightMapWidth)// if the brush goes off the posative end of the x axis we set the mod == to this
        {
            AOExMod1 = x + areaSize - terrainHeightMapWidth;
        }

        if (z < 0)//same as with x
        {
            AOEzMod = z;
        }
        else if (z + areaSize > terrainHeightMapHeight)
        {
            AOEzMod1 = z + areaSize - terrainHeightMapHeight;
        }
        if (effectType != EffectType.paint) // the following code will apply the terrain height modifications
        {
            // this grabs the heightmap values within the brushes area of effect
            heights = terrain.terrainData.GetHeights(x - AOExMod, z - AOEzMod, areaSize + AOExMod - AOExMod1, areaSize + AOEzMod - AOEzMod1); 
            terrainHeightMap = GetCurrentTerrainHeightMap();
        }
        
        switch (effectType)
        {
            case EffectType.raise:
                RaiseMap(x, z, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
            case EffectType.lower:
                LowerMap(x, z, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
            case EffectType.flatten:
                FlattenMap(x, z, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
            case EffectType.smooth:
                SmoothMap(x, z, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
            case EffectType.paint:
                PaintMap(x, z, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
            case EffectType.heightSnap:
                //ModifyHeight(x, z, y, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
        }
    }

    //Raise Terrain
    void RaiseMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < heightAdjustmentArea + AOEzMod - AOEzMod1; xx++)
        {
            for (int yy = 0; yy < heightAdjustmentArea + AOExMod - AOExMod1; yy++)
            {
                heights[xx, yy] += heightBrush[xx - AOEzMod, yy - AOExMod] * smoothStrength; //for each point we raise the value  by the value of brush at the coords * the strength modifier
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights); // This bit of code will save the change to the Terrain data file, this means that the changes will persist out of play mode into the edit mode
    }

    //Lower Terrain, just the reverse of raise terrain
    void LowerMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < heightAdjustmentArea + AOEzMod; xx++)
        {
            for (int yy = 0; yy < heightAdjustmentArea + AOExMod; yy++)
            {
                heights[xx, yy] -= heightBrush[xx - AOEzMod, yy - AOExMod] * smoothStrength;
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
    }

    //this moves the current value towards our target value to flatten terrain
    void FlattenMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < heightAdjustmentArea + AOEzMod; xx++)
        {
            for (int yy = 0; yy < heightAdjustmentArea + AOExMod; yy++)
            {
                heights[xx, yy] = Mathf.MoveTowards(heights[xx, yy], flattenHeight / 600, heightBrush[xx - AOEzMod, yy - AOExMod] * smoothStrength);
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
    }

    //Takes the average of surrounding points and moves the point towards that height
    void SmoothMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        int width = heightAdjustmentArea + AOEzMod - AOExMod1;
        int height = heightAdjustmentArea + AOExMod - AOExMod1;
        Vector2 mid = new Vector2(x + width / 2, z + height / 2);
        //GetSurroundingHeights(heights, x, z );
        for (int xx = x; xx < x + width; xx++)
        {
            for (int yy = z; yy < z + height; yy++)
            {
                //heightAvg[xx, yy] = GetSurroundingHeights(heights, xx, yy); // calculates the value we want each point to move towards
                float dist = Vector2.Distance(mid, new Vector2(xx, yy));
                float distRatio = dist / Mathf.Sqrt((width / 2) * (width / 2) + (height / 2) * (height / 2));
                float h = terrainHeightMap[xx, yy];
                terrainHeightMap[xx, yy] = h - (h - GetSurroundingHeights(terrainHeightMap, xx, yy)) * distRatio * smoothStrength;
            }
        }
        /*for (int xx1 = 0; xx1 < heightAdjustmentArea + AOEzMod; xx1++)
        {
            for (int yy1 = 0; yy1 < heightAdjustmentArea + AOExMod; yy1++)
            {
                heights[xx1, yy1] = Mathf.MoveTowards(heights[xx1, yy1], heightAvg[xx1, yy1], heightBrush[xx1 - AOEzMod, yy1 - AOExMod] * heightStrength); // moves the points towards their targets
            }
        }*/
        terrain.terrainData.SetHeights(0, 0, terrainHeightMap);
    }

    //Paint terrain
    void PaintMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        splat = terrain.terrainData.GetAlphamaps(x - AOExMod, z - AOEzMod, areaOfEffectSize + AOExMod, areaOfEffectSize + AOEzMod); //grabs the splat map data for our brush area
        for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
        {
            for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
            {
                float[] weights = new float[terrain.terrainData.alphamapLayers]; //creates a float array and sets the size to be the number of paints your terrain has
                for (int zz = 0; zz < splat.GetLength(2); zz++)
                {
                    weights[zz] = splat[xx, yy, zz];//grabs the weights from the terrains splat map
                }
                weights[paint] += paintBrush[xx - AOEzMod, yy - AOExMod] * paintStrength * 2000; // adds weight to the paint currently selected with the int paint variable
                                                                                       //this next bit normalizes all the weights so that they will add up to 1
                float sum = GetSumOfFloats(weights);
                for (int ww = 0; ww < weights.Length; ww++)
                {
                    weights[ww] /= sum;
                    splat[xx, yy, ww] = weights[ww];
                }
            }
        }
        //applies the changes to the terrain, they will also persist
        terrain.terrainData.SetAlphamaps(x - AOExMod, z - AOEzMod, splat);
        terrain.Flush();
    }

    /// <summary>
    /// Adjusts the height at the location
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <param name="y"></param>
    public void ModifyHeight(int x, int z, float y)
    {
        //These AreaOfEffectModifier variables below will help us if we are modifying terrain that goes over the edge,
        //you will see in a bit that I use Xmod for the the z(or Y) values, which was because I did not realize at first
        //that the terrain X and world X is not the same so I had to flip them around and was too lazy to correct the names, so don't get thrown off by that.
        int AOExMod = 0;
        int AOEzMod = 0;
        int AOExMod1 = 0;
        int AOEzMod1 = 0;
        if (x < 0) // if the brush goes off the negative end of the x axis we set the mod == to it to offset the edited area
        {
            AOExMod = x;
        }
        else if (x + heightAdjustmentArea > terrainHeightMapWidth)// if the brush goes off the posative end of the x axis we set the mod == to this
        {
            AOExMod1 = x + heightAdjustmentArea - terrainHeightMapWidth;
        }

        if (z < 0)//same as with x
        {
            AOEzMod = z;
        }
        else if (z + heightAdjustmentArea > terrainHeightMapHeight)
        {
            AOEzMod1 = z + heightAdjustmentArea - terrainHeightMapHeight;
        }
        int width = heightAdjustmentArea + AOEzMod - AOEzMod1;
        int height = heightAdjustmentArea + AOExMod - AOExMod1;
        heights = terrain.terrainData.GetHeights(x - AOExMod, z - AOEzMod, height, width);

        bool raising = y > undoHeightHolder[x, z];

        // adjusts heights in area
        for (int xx = 0; xx < width; xx++)
        {
            for (int yy = 0; yy < height; yy++)
            {
                float h = heights[xx, yy];
                float diff = y - h;
                float dist = Mathf.Sqrt((Mathf.Abs(xx - width / 2) * Mathf.Abs(xx - width / 2)) + (Mathf.Abs(yy - height / 2) * Mathf.Abs(yy - height / 2)));
                float distRatio = dist / Mathf.Sqrt((width / 2) * (width / 2) + (height / 2) * (height / 2));
                float yRatio = y - (diff * distRatio * heightAdjustmentSlope);
                if (raising)
                {
                    heights[xx, yy] = Mathf.Max(yRatio, h); // use higher height so we don't override previous height raise with lower
                } else
                {
                    heights[xx, yy] = Mathf.Min(yRatio, h); // use lower height so we don't override previous height lower with raise
                }
                //heights[xx, yy] = yRatio; //for each point we raise the value  by the value of brush at the coords * the strength modifier
            }
        }

        //heights[x, z] = y;
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
    }

    public void StartPainting()
    {
        if (terrainPaint) StartTerrainPaint();
        if (snapHeight) StartHeightAdjustment();
        painting = true;
    }

    public void StartTerrainPaint()
    {
        undoSplatHolder = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
    }

    public void UndoTerrainPaint()
    {
        terrain.terrainData.SetAlphamaps(0, 0, undoSplatHolder);
    }
    
    public void StartHeightAdjustment()
    {
        undoHeightHolder = GetCurrentTerrainHeightMap();
    }

    public void UndoHeightAdjustment()
    {
        terrain.terrainData.SetHeights(0, 0, undoHeightHolder);
    }

    public void UndoPaint()
    {
        if (undoSplatHolder.Length > 0) UndoTerrainPaint();
        if (undoHeightHolder.Length > 0) UndoHeightAdjustment();
        terrain.Flush();
    }

    public void Bake()
    {
        undoSplatHolder = new float[0, 0, 0];
        undoHeightHolder = new float[0, 0];
        terrain.Flush();
        painting = false;
    }

    // steps through the bezier spline, every step casting to the terrain and painting at that location
    public void PaintAlongBezier()
    {
        Vector3 point;
        Ray ray;
        RaycastHit hit;
        int steps = stepsPerCurve * CurveCount;
        List<Vector2> hits = new List<Vector2>(); // to reuse for smoothing rather than recasting 
        for (int i = 0; i <= steps; i++)
        {
            point = GetPoint(i / (float)steps);
            ray = new Ray(point + Vector3.up * terrain.terrainData.size.y, Vector3.down);
            if (Physics.Raycast(ray, out hit)) // check if point on bezier actually above terrain
            {
                terrain = GetTerrainAtObject(hit.transform.gameObject);
                SetEditValues(terrain);
                GetTerrainCoordinates(hit, out int terX, out int terZ);
                effectType = EffectType.paint;
                if (terrainPaint) ModifyTerrain(Mathf.Max(0, terX - areaOfEffectSize / 2), Mathf.Max(0, terZ - areaOfEffectSize / 2));
                if (snapHeight)
                {
                    //painter.SetHeights();
                    float y = GetTerrainHeight(point.y);
                    hits.Add(new Vector2(terX, terZ));
                    //painter.ModifyHeight(terZ, terX, y);
                    //painter.effectType = TerrainPainter.EffectType.heightSnap;
                    ModifyHeight(Mathf.Max(0, terX - heightAdjustmentArea / 2), Mathf.Max(0, terZ - heightAdjustmentArea / 2), y);
                }
            }
        }
        /*if (snapHeight) // smooth
        {
            effectType = EffectType.smooth;
            foreach (Vector2 step in hits)
            {
                ModifyTerrain(Mathf.Max(0, (int)step.x - heightAdjustmentArea / 2), Mathf.Max(0, (int)step.y - heightAdjustmentArea / 2));
            }
        }*/
    }

    float GetSumOfFloats(float[] vals)
    {
        float sum = 0;
        foreach (float val in vals)
        {
            sum += val;
        }
        return sum;
    }
    #endregion
}
