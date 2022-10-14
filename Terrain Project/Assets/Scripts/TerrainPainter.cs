using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainPainter : BezierSpline
{
    #region Painting
    public Terrain terrain;
    TerrainData terrainData;

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
    float[,] brush; // this stores the brush.png pixel data
    public int areaOfEffectSize = 100; // size of the brush
    [Range(0.01f, 1f)] // you can remove this if you want
    public float strength; // brush strength
    public float flattenHeight = 0; // the height to which the flatten mode will go
    public EffectType effectType;
    public TerrainLayer paints;// a list containing all of the paints
    public int paint; // variable to select paint
    float[,,] splat; // A splat map is what unity uses to overlay all of your paints on to the terrain
    float[,,] undoSplatHolder = new float[0, 0, 0]; // holds splat map information from when we started editing for the purpose of reverting changes
    public int stepsPerCurve;
    public bool snapHeight;
    public bool painting;

    public float heightAdjustmentArea;
    public float heightAdjustmentSlope;

    Vector3 startPos;
    Vector3 endPos;
    
    // Start is called before the first frame update
    void Start()
    {
        brush = GenerateBrush(brushIMG, areaOfEffectSize);
        effectType = EffectType.paint;
        terrain = FindObjectOfType<Terrain>();
        terrainData = terrain.terrainData;
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
        terrainData = GetCurrentTerrainData();
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
        float avg = height[x, z]; // we will add all the heights to this and divide by int num bellow to get the average height
        int num = 1;
        for (int i = 0; i < 4; i++) //this will loop us through the possible surrounding spots
        {
            try // This will try to run the code bellow, and if one of the coords is not on the terrain(ie we are at an edge) it will pass the exception to the Catch{} below
            {
                // These give us the values surrounding the point
                if (i == 0)
                { value = height[x + 1, z]; }
                else if (i == 1)
                { value = height[x - 1, z]; }
                else if (i == 2)
                { value = height[x, z + 1]; }
                else
                { value = height[x, z - 1]; }
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
        return terrainData.size;
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
            terrainData.heightmapResolution,
            terrainData.heightmapResolution);
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
            return terrainData.heightmapResolution;
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
            return terrainData.heightmapResolution;
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
        var heightScale = 1.0f / terrainData.size.y;
        return yVal * heightScale;
    }

    /// <summary>
    /// Generates a brush for use of painting the terrain
    /// </summary>
    /// <param name="texture"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public float[,] GenerateBrush(Texture2D texture, int size)
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
        brush = heightMap;
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
        brush = GenerateBrush(brushIMG, areaOfEffectSize); // regenerates the brush with new size
    }
    public void SetBrushStrength(float value)//same idea as SetBrushSize()
    {
        strength += value;
        if (strength > 1)
        { strength = 1; }
        else if (strength < 0.01f)
        { strength = 0.01f; }
    }
    public void SetBrush()
    {
        brush = GenerateBrush(brushIMG, areaOfEffectSize);
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
        if (x < 0) // if the brush goes off the negative end of the x axis we set the mod == to it to offset the edited area
        {
            AOExMod = x;
        }
        else if (x + areaOfEffectSize > terrainHeightMapWidth)// if the brush goes off the posative end of the x axis we set the mod == to this
        {
            AOExMod1 = x + areaOfEffectSize - terrainHeightMapWidth;
        }

        if (z < 0)//same as with x
        {
            AOEzMod = z;
        }
        else if (z + areaOfEffectSize > terrainHeightMapHeight)
        {
            AOEzMod1 = z + areaOfEffectSize - terrainHeightMapHeight;
        }
        if (effectType != EffectType.paint) // the following code will apply the terrain height modifications
        {
            // this grabs the heightmap values within the brushes area of effect
            heights = terrain.terrainData.GetHeights(x - AOExMod, z - AOEzMod, areaOfEffectSize + AOExMod - AOExMod1, areaOfEffectSize + AOEzMod - AOEzMod1); 
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
                ModifyHeight(x, z, y, AOExMod, AOEzMod, AOExMod1, AOEzMod1);
                break;
        }
    }

    //Raise Terrain
    void RaiseMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < areaOfEffectSize + AOEzMod - AOEzMod1; xx++)
        {
            for (int yy = 0; yy < areaOfEffectSize + AOExMod - AOExMod1; yy++)
            {
                heights[xx, yy] += brush[xx - AOEzMod, yy - AOExMod] * strength; //for each point we raise the value  by the value of brush at the coords * the strength modifier
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights); // This bit of code will save the change to the Terrain data file, this means that the changes will persist out of play mode into the edit mode
    }

    //Lower Terrain, just the reverse of raise terrain
    void LowerMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
        {
            for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
            {
                heights[xx, yy] -= brush[xx - AOEzMod, yy - AOExMod] * strength;
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
    }

    //this moves the current value towards our target value to flatten terrain
    void FlattenMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
        {
            for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
            {
                heights[xx, yy] = Mathf.MoveTowards(heights[xx, yy], flattenHeight / 600, brush[xx - AOEzMod, yy - AOExMod] * strength);
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
    }

    //Takes the average of surrounding points and moves the point towards that height
    void SmoothMap(int x, int z, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        float[,] heightAvg = new float[heights.GetLength(0), heights.GetLength(1)];
        for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
        {
            for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
            {
                heightAvg[xx, yy] = GetSurroundingHeights(heights, xx, yy); // calculates the value we want each point to move towards
            }
        }
        for (int xx1 = 0; xx1 < areaOfEffectSize + AOEzMod; xx1++)
        {
            for (int yy1 = 0; yy1 < areaOfEffectSize + AOExMod; yy1++)
            {
                heights[xx1, yy1] = Mathf.MoveTowards(heights[xx1, yy1], heightAvg[xx1, yy1], brush[xx1 - AOEzMod, yy1 - AOExMod] * strength); // moves the points towards their targets
            }
        }
        terrain.terrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
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
                weights[paint] += brush[xx - AOEzMod, yy - AOExMod] * strength * 2000; // adds weight to the paint currently selected with the int paint variable
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
    public void ModifyHeight(int x, int z, float y, int AOExMod = 0, int AOEzMod = 0, int AOExMod1 = 0, int AOEzMod1 = 0)
    {
        heights[x, z] = y;
        terrain.terrainData.SetHeights(0, 0, heights);
    }

    public void StartPainting()
    {
        undoSplatHolder = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
        if (snapHeight) undoHeightHolder = GetCurrentTerrainHeightMap();
        painting = true;
    }

    public void UndoPaint()
    {
        if (undoSplatHolder.Length > 0)
        {
            terrain.terrainData.SetAlphamaps(0, 0, undoSplatHolder);
            if (snapHeight) terrain.terrainData.SetHeights(0, 0, undoHeightHolder);
            terrain.Flush();
        }
    }

    public void Bake()
    {
        undoSplatHolder = new float[0, 0, 0];
        undoHeightHolder = new float[0, 0];
        terrain.Flush();
        painting = false;
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
