using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using Random = UnityEngine.Random;

public class TableInterpreter : MonoBehaviour
{
    //layer of the copy from HoloLens' recognized Mesh
    public LayerMask meshLayer;

    //Visual-Debugging objects and settings
    public VisualDebugging visualDebugging;
    public bool DebugClusterInfo;
    public bool DebugRaysOnEdges;
    public GameObject[] debuggingObjects;
    public GameObject heightDebugCube;

    //settings
    [HideInInspector] public readonly float rayInterval = 0.025f;
    [HideInInspector] public readonly int firstAbstraction = 6;
    [HideInInspector] public readonly float gridRadius = 3;
    [HideInInspector] public int rayDimension { get; private set; }
    [HideInInspector] public float floorLevel { get; private set; }

    //storage, calculated Tile infrastructure
    [HideInInspector] public Tile[,] TileHolder { get; private set; }
    [HideInInspector] public List<List<TwoInt>> TileClusters { get; private set; }
    [HideInInspector] public List<ExtraClusterInfo> ExtraClustersInfo { get; private set; }

    //computing vars
    List<TwoInt> currentCluster;
    ExtraClusterInfo currentExtraInfo;
    int _clusterIndex = 0;
    List<TwoInt> toCheck0;
    List<TwoInt> toCheck1;
    int currentlyCleared = 0;
    bool[,] TriedToAddOnce;

    //setup of lists and vars
    void Start()
    {
        rayDimension = (int)(2 * gridRadius / rayInterval);
        TileHolder = new Tile[rayDimension, rayDimension];
        TileClusters = new List<List<TwoInt>>();
        currentCluster = new List<TwoInt>();
        ExtraClustersInfo = new List<ExtraClusterInfo>();
        currentExtraInfo = new ExtraClusterInfo();
        toCheck0 = new List<TwoInt>();
        toCheck1 = new List<TwoInt>();
        TriedToAddOnce = new bool[rayDimension,rayDimension];
        //Debug.Log("holder.length is: " + tileHolder[].Length+"    and should be: "+holderSize);
    }

    public void StartTableInterpretation(float floorlevel)
    {
        floorLevel = floorlevel;
        //a rough grid searches for Parts of a Table. If it finds a Part, a cluster of fine tiles is spread from its position and registers correlation
        for(int xRough=0; xRough < rayDimension; xRough += firstAbstraction)
        {
            for (int zRough = 0; zRough < rayDimension; zRough += firstAbstraction)
            {

                TryToAdd(xRough, zRough);
                while (toCheck0.Count > 0 || toCheck1.Count > 0)
                {
                    if (currentlyCleared == 0)
                    {
                        while (toCheck0.Count > 0) //NOTE: might be more performant to run with a for loop backwards
                        {
                            CheckAndCollectNeighbours(toCheck0[0]);
                        }
                        currentlyCleared = 1;
                    }
                    else
                    {
                        while (toCheck1.Count > 0)//NOTE: same here
                        {
                            CheckAndCollectNeighbours(toCheck1[0]);
                        }
                        currentlyCleared = 0;
                    }
                }
                if (currentCluster.Count > 0)
                {
                    TileClusters.Add(new List<TwoInt>(currentCluster));
                    int elementCount = currentCluster.Count;
                    currentExtraInfo.SetAverageX(currentExtraInfo.addedXi / elementCount * rayInterval - gridRadius);
                    currentExtraInfo.SetAverageZ(currentExtraInfo.addedZi / elementCount * rayInterval - gridRadius);
                    currentExtraInfo.SetAverageH(currentExtraInfo.addedH / elementCount);
                    //(note: unlike classes structs always return copies and not references)
                    ExtraClustersInfo.Add(currentExtraInfo);
                    currentCluster.Clear();
                    currentExtraInfo = new ExtraClusterInfo();
                    //Debug.Log("created new cluster: " + _clusterIndex);
                    _clusterIndex++;
                }
            }
        }

        //tile clusters were detected,
        //this now calculates further properties of the tiles within those clusters
        CalcAllEdgenessesEtc();
        CalcEdgeDistEtc();

        //visual Debugging
        if (visualDebugging != VisualDebugging.no) VisualyDebugAll();
        if (DebugClusterInfo) VisualyDebugClusterInfo();
    }

    void CalcEdgeDistEtc()
    {

        for(int borderType=0; borderType < 3; borderType++)
        {
            //bool byHill = (i == 0);
            TriedToAddOnce = new bool[rayDimension, rayDimension];
            int clusterIndex = 0;
            foreach (List<TwoInt> cluster in TileClusters)
            {
                currentlyCleared = 0;
                int maxDist = -1; //this can be the maximum distToHill or distToRift
                foreach (TwoInt tile in cluster)
                {
                    int xi = tile.xi;
                    int zi = tile.zi;
                    
                    //Add Tiles on Edges/Hills/Rifts into the "to-check"-list
                    if (TileHolder[xi, zi].edgeness > 0 && borderType==0 || TileHolder[xi, zi].hillness > 0 && borderType ==1 || TileHolder[xi, zi].riftness >0 && borderType ==2)
                    {

                        if (DebugRaysOnEdges)
                        {
                            int c = TileHolder[xi, zi].clusterIndex % 4;
                            Color clusterColor = c == 0 ? Color.gray : c == 1 ? Color.green : c == 2 ? Color.blue : Color.red;
                            Debug.DrawRay(new Vector3(xi * rayInterval - gridRadius, TileHolder[xi, zi].h + floorLevel, zi * rayInterval - gridRadius), Vector3.up, clusterColor, 1000000);
                        }

                        toCheck0.Add(new TwoInt(xi, zi));
                        TriedToAddOnce[xi, zi]=true;
                    }
                }


                while (toCheck0.Count > 0 || toCheck1.Count > 0)
                {
                    maxDist++;
                    if (currentlyCleared == 0)
                    {
                        while (toCheck0.Count > 0) //NOTE: might be more performant to run with a for loop backwards
                        {
                            SetDistAndAddNeighbours(toCheck0[0], borderType);
                        }
                        currentlyCleared = 1;
                    }
                    else
                    {
                        while (toCheck1.Count > 0)//NOTE: same here
                        {
                            SetDistAndAddNeighbours(toCheck1[0], borderType);
                        }
                        currentlyCleared = 0;
                    }
                }
                ExtraClusterInfo extraInfo = ExtraClustersInfo[clusterIndex];

                //Debug.Log("maxDist of Cluster " + clusterIndex + " is " + maxDist);
                if (borderType == 0) extraInfo.maxEdgeDist = maxDist;
                else if(borderType == 1) extraInfo.maxHillDist=maxDist;
                else if (borderType == 2) extraInfo.maxRiftDist = maxDist;
                ExtraClustersInfo[clusterIndex] = extraInfo;
                //Debug.Log("recheck:" + (byHill?extraClustersInfo[clusterIndex].maxHillDist: extraClustersInfo[clusterIndex].maxRiftDist));
                clusterIndex++;
            }

            
        }

        /* //This edgeDist calculation below was replaced by the less performant but consistent to the outer border calculation above
        int clustersIndex = 0;
        foreach (List<TwoInt> cluster in tileClusters)
        {
            int maxEdgeDist = 0;
            foreach (TwoInt tile in cluster)
            {
                int xi = tile.xi;
                int zi = tile.zi;
                int distHill = tileHolder[xi, zi].distHill;
                int distRift = tileHolder[xi, zi].distRift;
                int distEdge = distHill < distRift ? distHill : distRift;
                tileHolder[xi, zi].distEdge = distEdge;
                if (distEdge > maxEdgeDist) maxEdgeDist = distEdge;
            }
            ExtraClusterInfo extraInfo = extraClustersInfo[clustersIndex];
            extraInfo.maxEdgeDist = maxEdgeDist;
            extraClustersInfo[clustersIndex] = extraInfo;
            clustersIndex++;
        }
        */
    }

    void SetDistAndAddNeighbours(TwoInt xz, int borderType)
    {
        int xi = xz.xi;
        int zi = xz.zi;

        int dist = borderType == 0 ? TileHolder[xi, zi].distEdge:
                   borderType == 1 ? TileHolder[xi, zi].distHill 
                                   : TileHolder[xi, zi].distRift;

        TryToAddForDist(dist, borderType, xi + 1, zi);
        TryToAddForDist(dist, borderType, xi - 1, zi);
        TryToAddForDist(dist, borderType, xi, zi + 1);
        TryToAddForDist(dist, borderType, xi, zi - 1);


        if (currentlyCleared == 0)
            toCheck0.Remove(xz);
        else
            toCheck1.Remove(xz);
    }
    void TryToAddForDist(int prevIndex, int borderType, int xi, int zi)
    {
        if (!IsOutOfBounds(xi, zi))
        {
            if (!TriedToAddOnce[xi, zi])
            {
                TriedToAddOnce[xi, zi] = true;
                if (TileHolder[xi, zi].state == State.fine)
                {
                    if (borderType == 0) TileHolder[xi, zi].distEdge = prevIndex + 1;
                    if (borderType == 1) TileHolder[xi, zi].distHill = prevIndex + 1;
                    else TileHolder[xi, zi].distRift = prevIndex + 1;

                    //this two lists swap with clearing and filling each other
                    if (currentlyCleared == 1) toCheck0.Add(new TwoInt(xi, zi));
                    else toCheck1.Add(new TwoInt(xi, zi));
                }

            }
        }
    }

    //does only return unchecked tiles and unchecked neighbours (and marks them as checked)
    void CheckAndCollectNeighbours(TwoInt xz)
    {
        int xi = xz.xi;
        int zi = xz.zi;

        TwoInt? testedTile;
        testedTile = TryCreateTile(xi, zi);
        if (testedTile!=null)
        {
            TryToAdd(xi + 1, zi);
            TryToAdd(xi - 1, zi);
            TryToAdd(xi, zi + 1);
            TryToAdd(xi, zi - 1);
            currentCluster.Add((TwoInt)testedTile);
        }


        if (currentlyCleared == 0)
            toCheck0.Remove(xz);
        else
            toCheck1.Remove(xz);


    }


    void TryToAdd(int xi, int zi)
    {
        //Debug.Log("beforeBoundcheck");
        if (!IsOutOfBounds(xi, zi))
        {
            //Debug.Log("afterBoundcheck");
            if (!TriedToAddOnce[xi, zi])
            {
                TriedToAddOnce[xi, zi] = true;
                if (!TileHolder[xi, zi].tested)
                {
                    //Debug.Log("tryToAdd: " + xi + "," + zi + "   currentlyCleared is " + currentlyCleared);
                    if (currentlyCleared == 1) toCheck0.Add(new TwoInt(xi, zi));
                    else toCheck1.Add(new TwoInt(xi, zi));
                    //Debug.Log("afterTryToAdd");
                }
            }
        }
        //else Debug.Log("tryToAdd: " + xi + "," + zi + " but it was out of bounds");
    }

    TwoInt? TryCreateTile(int xi, int zi)
    {
        //Debug.Log("checktile at " + xi + "," + zi);
        if (IsOutOfBounds(xi,zi)) return null; //prevent index out of bounds
        //Debug.Log("in bound checktile at " + xi + "," + zi);
        if (TileHolder[xi, zi].tested) return null;
        //Debug.Log("untested tile at " + xi + "," + zi);
        RaycastHit hitInfo;

        if (Physics.Raycast(new Vector3(xi * rayInterval - gridRadius, floorLevel + 2, zi * rayInterval - gridRadius), Vector3.down, out hitInfo, 3, meshLayer))
        {
            //Debug.Log("try create tile at " + xi + "," + zi);
            Tile testedTile = new Tile(hitInfo.point.y - floorLevel, xi, zi, _clusterIndex);
            TileHolder[xi, zi] = testedTile;
            if (testedTile.state == State.fine)
            {
                //Debug.Log("try visualyDebug tile at " + xi + "," + zi);

                //currentExtraInfo does not contain infomration of one specific tile.
                //It is later used to calculate the average position of a cluster, which contains many of such tiles.
                currentExtraInfo.AddXi(xi);
                currentExtraInfo.AddZi(zi);
                currentExtraInfo.AddH(hitInfo.point.y - floorLevel);

                return new TwoInt(testedTile.xi, testedTile.zi);
            }
            else
            {
                return null;
            }
        }
        else { 



            return null; 
        }
    }

    public bool IsOutOfBounds(int xi, int zi)
    {
        if (xi < 0 || zi < 0 || xi > rayDimension-1 || zi > rayDimension-1) return true;
        else return false;
    }


    void CalcAllEdgenessesEtc()
    {
        foreach(List<TwoInt> cluster in TileClusters)
        {
            foreach(TwoInt tile in cluster)
            {
                int tileXi = tile.xi;
                int tileZi = tile.zi;
                SetEdgenessEtcByNeighbour(tileXi, tileZi, 1, 0);
                SetEdgenessEtcByNeighbour(tileXi, tileZi, -1, 0);
                SetEdgenessEtcByNeighbour(tileXi, tileZi, 0, 1);
                SetEdgenessEtcByNeighbour(tileXi, tileZi, 0, -1);
                //VisuallyDebugTile(tileXi, tileZi);
            }
        }
    }

    void SetEdgenessEtcByNeighbour(int xi, int zi, int changeX, int changeZ)
    {
        int neighbourX = xi + changeX;
        int neighbourZ = zi + changeZ;
        Tile tile = TileHolder[xi, zi];
        if (IsOutOfBounds(neighbourX, neighbourZ)) TileHolder[xi, zi].edgeness++;
        else
        {
            if (TileHolder[neighbourX, neighbourZ].state == State.tooHigh)
            {
                TileHolder[xi, zi].edgeness++;
                TileHolder[xi, zi].hillness++;
                //Debug.Log("Set hillness to: "+ tileHolder[xi, zi].hillness);
            }
            else if (TileHolder[neighbourX, neighbourZ].state == State.tooLow)
            {
                TileHolder[xi, zi].edgeness++;
                TileHolder[xi, zi].riftness++;
                //Debug.Log("Set riftness to: " + tileHolder[xi, zi].riftness);
                
            }
            //this happens when there was no terrain at the tiles position because of gaps in the Mesh from HoloLens
            else if (TileHolder[neighbourX, neighbourZ].state == State.notTested) 
            {
                TileHolder[xi, zi].edgeness++;
                //Debug.LogError("Checking unchecked Tile as neighbour");
            }
        }
    }


    void VisuallyDebugTile(int xi, int zi)
    {
        Tile tile = TileHolder[xi,zi];
        int selectIndex = 0;
        if (visualDebugging == VisualDebugging.byCluster)
        {
            selectIndex = tile.clusterIndex % debuggingObjects.Length;
        }else if (visualDebugging == VisualDebugging.byEdgeness)
        {
            selectIndex = tile.edgeness % debuggingObjects.Length;
        }
        else if (visualDebugging == VisualDebugging.byHillness)
        {
            selectIndex = tile.hillness % debuggingObjects.Length;
        }
        else if (visualDebugging == VisualDebugging.byRiftness)
        {
            selectIndex = tile.riftness % debuggingObjects.Length;
        }

        else if (visualDebugging == VisualDebugging.byEdgeDist)
        {
            selectIndex = tile.distEdge / 2 % debuggingObjects.Length;
        }
        else if (visualDebugging == VisualDebugging.byHillDist)
        {
            selectIndex = tile.distHill / 2 % debuggingObjects.Length;
        }
        else if (visualDebugging == VisualDebugging.byRiftDist)
        {
            selectIndex = tile.distRift / 2 % debuggingObjects.Length;
        }
        Instantiate(debuggingObjects[selectIndex], new Vector3(xi * rayInterval - gridRadius, tile.h + floorLevel, zi * rayInterval - gridRadius), Quaternion.identity);
    }

    void VisualyDebugAll()
    {
        foreach (List<TwoInt> cluster in TileClusters)
        {
            foreach (TwoInt tile in cluster)
            {
                VisuallyDebugTile(tile.xi, tile.zi);
            }
        }

        if (DebugClusterInfo)
        {
            int maxDist=-1;
            foreach (ExtraClusterInfo clusterInfo in ExtraClustersInfo)
            {
                ExtraClusterInfo info = clusterInfo;
                if (visualDebugging == VisualDebugging.byEdgeDist) maxDist = info.maxEdgeDist;
                else if (visualDebugging == VisualDebugging.byHillDist) maxDist = info.maxHillDist;
                else if (visualDebugging == VisualDebugging.byRiftDist) maxDist = info.maxRiftDist;
                Debug.Log("height is " + maxDist);
                for (int i = maxDist; i >= 0; i--)
                {
                    Instantiate(heightDebugCube, new Vector3(info.averageX, info.averageH + floorLevel + (i * 0.1f), info.averageZ), Quaternion.identity);
                }
            }
        }

    }

    void VisualyDebugClusterInfo()
    {
        foreach(ExtraClusterInfo info in ExtraClustersInfo)
        {
            int height = info.maxEdgeDist;
            for(int i = height; i>=0; i--)
            {
                Instantiate(heightDebugCube, new Vector3(info.averageX, info.averageH + floorLevel + height*0.1f, info.averageZ), Quaternion.identity);
            }
        }
    }

    public float IAsF(int i)
    {
        return i * rayInterval - gridRadius;
    }

    public int FAsI(float f)
    {
        return (int)Mathf.Round((f+gridRadius)/rayInterval);
    }


    //for parameters "towards" and "avoid" following int has following meaning:
    //-1 = nothing
    // 0 = edge
    // 1 = hill
    // 2 = rift
    public List<TwoInt> FindPathOut(TwoInt startingPoint, int towards, int avoid)//, TwoInt? preferredDir)
    {
        //bool hasPrefDir = (preferredDir != null);
        int prevDir = -1; //-1=noPrevDir 0=up, 1=right, 2=down, 3=left
        List<TwoInt> path = new List<TwoInt>();
        int currentDist = towards==0? TileHolder[startingPoint.xi, startingPoint.zi].distEdge:
                          towards==1? TileHolder[startingPoint.xi, startingPoint.zi].distHill:
                                      TileHolder[startingPoint.xi, startingPoint.zi].distRift;
        //currentDist--;
        path.Add(startingPoint);
        TwoInt prevTile = startingPoint;
        TwoInt[] neighbours = new TwoInt[4];
        
        //bool reachedEdge = false;
        while (currentDist>0) //MAYBE CHANGE TO 0 AND SEARCH FOR OTHER ERROER!!!!!!!!!!!!!!!!!!!!!!
        {
            //Debug.Log("current dist at pathOutFinding: " + currentDist);
            bool[] options = new bool[] {true,true,true,true};
            //filter out previous direction from possible directions
            if (prevDir > -1) options[prevDir] = false;

            //filter out directions which are not going towards goal or are out of bounds
            int invalidOptions = 0;
            for(int i=0; i<4; i++)
            {
                if (options[i])
                {
                    TwoInt dir = Dir(i);
                    TwoInt pos = new TwoInt(prevTile.xi + dir.xi, prevTile.zi + dir.zi);
                    neighbours[i] = pos;
                    if (IsOutOfBounds(pos.xi, pos.zi))
                    {
                        options[i] = false;
                        invalidOptions++;
                    }
                    else
                    {
                        int checkedDist = towards == 0 ? TileHolder[pos.xi, pos.zi].distEdge :
                                          towards == 1 ? TileHolder[pos.xi, pos.zi].distHill :
                                                         TileHolder[pos.xi, pos.zi].distRift;

                        if (checkedDist > currentDist-1)
                        {
                            options[i] = false;
                            invalidOptions++;
                        }
                    }
                }
                else invalidOptions++;
            }

            if (invalidOptions > 3) Debug.LogError("Something went wrong with finding a pathing out of a cluster");

            //filter out directions by avoiding something (like: try to keep distance to hills high)
            if (avoid > -1)
            {
                int highestAvoidRating = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (options[i])
                    {
                        Tile checkedTile = TileHolder[neighbours[i].xi, neighbours[i].zi];
                        int rating = avoid == 0 ? checkedTile.distEdge :
                                     avoid == 1 ? checkedTile.distHill :
                                                  checkedTile.distRift;
                        if (rating > highestAvoidRating) highestAvoidRating = rating;
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    if (options[i])
                    {
                        Tile checkedTile = TileHolder[neighbours[i].xi, neighbours[i].zi];
                        int rating = avoid == 0 ? checkedTile.distEdge :
                                     avoid == 1 ? checkedTile.distHill :
                                                  checkedTile.distRift;
                        if(rating < highestAvoidRating)
                        {
                            options[i] = false;
                            invalidOptions++;
                        }
                    }
                }
            }
            if (invalidOptions > 3) Debug.LogError("Something went wrong with at rating the avoid options for pathing out of a cluster");

            List<int> leftoverDirs = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                if (options[i])
                {
                    leftoverDirs.Add(i);
                }
            }
            int chosenDir = leftoverDirs[Random.Range(0, leftoverDirs.Count)];
            path.Add(neighbours[chosenDir]);
            prevTile = neighbours[chosenDir];
            switch (chosenDir)
            {
                case 0: prevDir = 2;break;
                case 1: prevDir = 3;break;
                case 2: prevDir = 0;break;
                case 3: prevDir = 1;break;
            }

            currentDist--;
        }








        return path;
    }

    TwoInt Dir(int dirInt)
    {
        //returned directions depending on dirInt:  0=up, 1=right, 2=down, 3=left
        return new TwoInt(dirInt == 1 ? 1 : dirInt == 3 ? -1 : 0, dirInt == 0 ? -1 : dirInt == 2 ? 1 : 0);
    }


    public struct Tile
    {
        public bool tested;
        public float h;
        public int xi;
        public int zi;
        //public float fromX;
        //public float fromZ;
        public State state;
        public bool valid;
        //public int distToEdge;
        public int clusterIndex;

        public int edgeness;
        public int hillness;
        public int riftness;

        public int distEdge;
        public int distHill;
        public int distRift;
        public Tile(float h, int xi, int zi, int clusterIndex)
        {
            tested = true;
            this.h = h;
            this.xi = xi;
            this.zi = zi;
            //this.fromX = fromX;
            //this.fromZ = fromZ;
            state = h < 0.65f ? State.tooLow : h < 1.0f ? State.fine : State.tooHigh;
            valid = (state == State.fine);
            //distToEdge = -1;
            this.clusterIndex = clusterIndex;

            edgeness = 0;
            hillness = 0;
            riftness = 0;

            distEdge = 0;
            distHill = 0;
            distRift = 0;
        }
    }

    public struct TwoInt
    {
        public int xi;
        public int zi;
        public TwoInt(int xi, int zi)
        {
            this.xi = xi;
            this.zi = zi;
        }
    }

    public struct ExtraClusterInfo
    {
        public int maxEdgeDist, maxHillDist, maxRiftDist;
        public float addedXi, averageX;
        public float addedZi, averageZ;
        public float addedH, averageH;

        //public void TrySetMaxEdgeDist(int testedDist)
        //{
        //    if (testedDist > maxEdgeDist) maxEdgeDist = testedDist;
        //}
        //public void TrySetMaxHillDist(int testedDist)
        //{
        //    if (testedDist > maxHillDist) maxHillDist = testedDist;
        //}
        //public void TrySetMaxRiftDist(int testedDist)
        //{
        //    if (testedDist > maxRiftDist) maxRiftDist = testedDist;
        //}
        public void SetMaxEdgeDist(int dist) { maxEdgeDist = dist; }
        public void SetMaxHillDist(int dist) { maxHillDist = dist; }
        public void SetMaxRiftDist(int dist) { maxRiftDist = dist; }

        public void AddXi(float xi) { addedXi += xi; }
        public void AddZi(float zi) { addedZi += zi; }
        public void AddH(float h) { addedH += h; }
        public void SetAverageX(float x) { averageX = x; }
        public void SetAverageZ(float z) { averageZ = z; }
        public void SetAverageH(float h) { averageH = h; }
    }

    public enum State
    {
        notTested,
        fine,
        tooHigh,
        tooLow
    }


    enum CheckResult
    {
        testedFine,
        testedBad,
        fine,
        bad
        
    }

    public enum VisualDebugging
    {
        no,
        byCluster,
        byEdgeDist,
        byHillDist,
        byRiftDist,
        byEdgeness,
        byHillness,
        byRiftness
    }
}
