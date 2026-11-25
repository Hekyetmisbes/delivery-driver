using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DeliveryDriver.City;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    internal sealed partial class ProceduralWorldPlanner
    {
        [Serializable] internal sealed class SavedNode
        {
            public int id,degree;
            public Vector3 position;
            public string sector;
            public bool gate;
        }
        [Serializable] internal sealed class SavedEdge
        {
            public int a,b;
            public List<Vector3> points;
            public RoadKind kind;
            public string sector;
            public bool spine;
        }
        [Serializable] internal sealed class SavedAccess
        {
            public string roadId,message;
            public float t;
            public Vector3 position;
            public bool connected;
        }
        [Serializable] internal sealed class PreviewSnapshot
        {
            public int format=1;
            public string fingerprint,scenePath,settingsDescription,version;
            public List<SavedNode> nodes;
            public List<SavedEdge> edges,detached,links;
            public List<TerrainAnalysisCell> terrain;
            public List<UrbanArea> parks,lots;
            public List<AnalysisLine> contours,seams,earthworks,zones;
            public List<Rect> protectedAreas;
            public List<Vector3> markers;
            public List<SavedAccess> access;
            public List<string> notes,warnings;
            public QualityReport quality;
            public List<string> stageReports;
            public bool previewCriteriaMet;
        }
        private static string ObjectId(UnityEngine.Object obj) =>
            obj==null?"":GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();

        internal static string DescribeSettings(Settings s)
        {
            var parts=new List<string>();
            foreach(var field in typeof(Settings).GetFields().OrderBy(f=>f.Name))
            {
                object value=field.GetValue(s);
                if(value is UnityEngine.Object obj)parts.Add(field.Name+"="+ObjectId(obj));
                else if(value is IFormattable formattable)parts.Add(field.Name+"="+formattable.ToString(null,CultureInfo.InvariantCulture));
                else if(field.Name!="accessAnchors")parts.Add(field.Name+"="+(value?.ToString()??"null"));
            }
            foreach(var a in s.accessAnchors)
                parts.Add("anchor="+ObjectId(a.road)+":"+a.t.ToString("R",CultureInfo.InvariantCulture));
            return string.Join("\n",parts);
        }

        // Exact input key for preview caching. Terrain height samples are hashed in
        // blocks, including unsaved edits; road centerline samples match obstacle
        // raster spacing. No asset writes. A key mismatch invalidates reuse/refinement.
        internal static string PreviewFingerprint(Settings s,Layout l,Scene scene)
        {
            using(var hash=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(stream))
            {
                writer.Write("delivery-driver-procedural-preview-v7.2-staged");
                writer.Write(scene.path??"");writer.Write(DescribeSettings(s));
                void Vec(Vector3 v){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                void Matrix(Matrix4x4 m){for(int i=0;i<16;i++)writer.Write(m[i]);}
                foreach(var tile in l.tiles.OrderBy(t=>t.row).ThenBy(t=>t.column))
                {
                    writer.Write(tile.row);writer.Write(tile.column);writer.Write(ObjectId(tile.terrain));
                    Vec(tile.terrain.transform.position);
                    var data=tile.terrain.terrainData;Vec(data.size);int n=data.heightmapResolution;writer.Write(n);
                    for(int z=0;z<n;z+=64)
                    {
                        float[,] rows=data.GetHeights(0,z,n,Mathf.Min(64,n-z));
                        byte[] bytes=new byte[rows.Length*sizeof(float)];
                        Buffer.BlockCopy(rows,0,bytes,0,bytes.Length);writer.Write(bytes);
                    }
                }
                var roots=scene.GetRootGameObjects();
                var roads=roots.SelectMany(r=>r.GetComponentsInChildren<SplineRoad>(true))
                    .Where(r=>r.IsUsable).ToList();
                writer.Write(roads.Count);
                foreach(var r in roads)
                {
                    writer.Write(ObjectId(r));writer.Write(r.name);
                    Matrix(r.transform.localToWorldMatrix);
                    float len=r.GetTotalLength();writer.Write(len);
                    int count=Mathf.Max(1,Mathf.CeilToInt(len/4f));writer.Write(count);
                    Vec(r.GetPoint(0f));
                    for(int i=1;i<=count;i++)Vec(r.GetPoint(r.DistanceToT(len*i/count)));
                }
                var zones=roots.SelectMany(r=>r.GetComponentsInChildren<NeighborhoodZone>(true))
                    .ToList();
                writer.Write(zones.Count);
                foreach(var zone in zones)
                {
                    writer.Write(zone.NeighborhoodName??zone.name);Matrix(zone.transform.localToWorldMatrix);
                    var box=zone.GetComponent<BoxCollider>();writer.Write(box!=null);
                    if(box!=null){Vec(box.center);Vec(box.size);}
                }
                foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ThirdParty/LowPolyCity/Prefab/Buildings"}).OrderBy(v=>v))
                {
                    writer.Write(guid);writer.Write(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(guid)).ToString());
                }
                writer.Flush();stream.FlushFinalBlock();
                return BitConverter.ToString(hash.Hash).Replace("-","");
            }
        }

        internal static PreviewSnapshot Capture(Plan p,string fingerprint,string scenePath,string settingsDescription)
        {
            var all=p.nodes.Concat(p.edges.Concat(p.detachedEdges).Concat(p.authoredLinks).SelectMany(e=>new[]{e.a,e.b})).Distinct().ToList();
            var map=all.Select((n,i)=>(n,i)).ToDictionary(x=>x.n,x=>x.i);
            List<SavedEdge> Edges(IEnumerable<Edge> source)=>source.Select(e=>new SavedEdge{
                a=map[e.a],b=map[e.b],points=new List<Vector3>(e.points),kind=e.kind,sector=e.sector,spine=e.spine}).ToList();
            return new PreviewSnapshot{fingerprint=fingerprint,scenePath=scenePath,settingsDescription=settingsDescription,
                version=p.version,nodes=all.Select(n=>new SavedNode{id=n.id,degree=n.degree,position=n.position,sector=n.sector,gate=n.gate}).ToList(),
                edges=Edges(p.edges),detached=Edges(p.detachedEdges),links=Edges(p.authoredLinks),
                terrain=p.terrainCells.ToList(),parks=p.openSpaces.ToList(),lots=p.parcels.ToList(),contours=p.contours.ToList(),
                seams=p.seams.ToList(),earthworks=p.earthworks.ToList(),zones=p.gradingZones.ToList(),protectedAreas=p.protectedAreas.ToList(),
                markers=p.gradingMarkers.ToList(),notes=p.notes.ToList(),warnings=p.warnings.ToList(),quality=p.quality,
                stageReports=p.stageReports.ToList(),previewCriteriaMet=p.previewCriteriaMet,
                access=p.accessResults.Select(a=>new SavedAccess{roadId=ObjectId(a.anchor?.road),t=a.anchor?.t??0f,
                    position=a.position,connected=a.connected,message=a.message}).ToList()};
        }

        internal static Plan Restore(PreviewSnapshot saved,Layout l,Settings s)
        {
            if(saved==null||saved.format!=1||saved.nodes==null||saved.edges==null||saved.detached==null||saved.links==null||
                saved.nodes.Count>100000||saved.edges.Count+saved.detached.Count+saved.links.Count>100000)
                throw new InvalidOperationException("Unsupported or invalid preview file.");
            bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&
                !float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
            if(saved.nodes.Any(n=>n==null||!Finite(n.position)))throw new InvalidOperationException("Invalid saved node.");
            var result=new Plan{layout=l,version=saved.version,quality=saved.quality};
            foreach(var n in saved.nodes)
                result.nodes.Add(new Node{id=n.id,degree=n.degree,position=n.position,sector=n.sector,gate=n.gate});
            void Edges(List<SavedEdge> source,List<Edge> target)
            {
                foreach(var e in source)
                {
                    if(e==null||e.a<0||e.a>=result.nodes.Count||e.b<0||e.b>=result.nodes.Count||e.points==null||e.points.Count<2||
                        e.points.Any(v=>!Finite(v))||e.points[0]!=result.nodes[e.a].position||e.points[e.points.Count-1]!=result.nodes[e.b].position)
                        throw new InvalidOperationException("Invalid saved edge geometry.");
                    target.Add(new Edge{a=result.nodes[e.a],b=result.nodes[e.b],points=new List<Vector3>(e.points),kind=e.kind,sector=e.sector,spine=e.spine});
                }
            }
            Edges(saved.edges,result.edges);Edges(saved.detached,result.detachedEdges);Edges(saved.links,result.authoredLinks);
            result.terrainCells.AddRange(saved.terrain??new List<TerrainAnalysisCell>());
            result.openSpaces.AddRange(saved.parks??new List<UrbanArea>());result.parcels.AddRange(saved.lots??new List<UrbanArea>());
            if(result.openSpaces.Concat(result.parcels).Any(a=>a==null||a.corners==null||a.corners.Length!=4||a.corners.Any(v=>!Finite(v))))
                throw new InvalidOperationException("Invalid saved area geometry.");
            result.contours.AddRange(saved.contours??new List<AnalysisLine>());result.seams.AddRange(saved.seams??new List<AnalysisLine>());
            result.earthworks.AddRange(saved.earthworks??new List<AnalysisLine>());result.gradingZones.AddRange(saved.zones??new List<AnalysisLine>());
            result.protectedAreas.AddRange(saved.protectedAreas??new List<Rect>());result.gradingMarkers.AddRange(saved.markers??new List<Vector3>());
            result.notes.AddRange(saved.notes??new List<string>());result.warnings.AddRange(saved.warnings??new List<string>());
            result.stageReports.AddRange(saved.stageReports??new List<string>());result.previewCriteriaMet=saved.previewCriteriaMet;
            foreach(var a in saved.access??new List<SavedAccess>())
            {
                var anchor=s.accessAnchors.FirstOrDefault(x=>ObjectId(x.road)==a.roadId&&Mathf.Abs(x.t-a.t)<.0001f);
                if(anchor==null)
                {
                    SplineRoad road=null;
                    if(!string.IsNullOrEmpty(a.roadId)&&GlobalObjectId.TryParse(a.roadId,out GlobalObjectId id))
                        road=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as SplineRoad;
                    anchor=new AccessAnchor{road=road,t=a.t};
                }
                result.accessResults.Add(new AnchorResult{anchor=anchor,position=a.position,connected=a.connected,message=a.message});
            }
            return result;
        }

        // Regional replan changes only whole non-backbone roads inside the selected
        // region. Endpoints, external geometry, bridge paths and authored links stay
        // fixed. This is conservative local rerouting, not new district topology.
        internal static Plan RefineRegion(Settings s,Layout l,Scene scene,Plan source,Rect region)
        {
            if(!s.qualityDesign||!s.urbanDesign)throw new InvalidOperationException("Enable V7 and V6 design before regional refinement.");
            // Deep clone through the serializable representation; never mutate a cached/saved plan.
            var clone=Restore(JsonUtility.FromJson<PreviewSnapshot>(JsonUtility.ToJson(Capture(source,"",scene.path,""))),l,s);
            var planner=new ProceduralWorldPlanner(s,l,scene,clone);
            clone.terrainCells.Clear();l.rawTerrainCache.Clear();planner.AnalyzeTerrain();
            clone.protectedAreas.Clear();clone.protectedAreas.AddRange(planner.protectedAreas);
            clone.manualRoadNear=planner.obstacles.Near;
            var index=new StreetIndex();
            foreach(var e in clone.edges.Concat(clone.authoredLinks))index.Add(e);
            var targets=clone.edges.Where(e=>!e.Bridge&&!e.spine&&e.kind!=RoadKind.Arterial&&
                !e.a.gate&&!e.b.gate&&e.points.All(v=>Contains(region,XZ(v),-planner.RoadClearance))&&
                !clone.authoredLinks.Any(a=>a.b==e.a||a.b==e.b))
                .OrderByDescending(e=>planner.QualityPathCost(e.points)).Take(180).ToList();
            int improved=0,processed=0;
            string current="";
            foreach(var e in targets)
            {
                if(++processed%12==0&&EditorUtility.DisplayCancelableProgressBar("Procedural World","Refining district "+processed+"/"+targets.Count,.45f))
                    throw new OperationCanceledException();
                Rect city=e.sector=="Ankara"?l.Ankara(s.ankaraLow):l.Istanbul(s.ankaraLow);
                Rect bounds=Rect.MinMaxRect(Mathf.Max(city.xMin,region.xMin),Mathf.Max(city.yMin,region.yMin),
                    Mathf.Min(city.xMax,region.xMax),Mathf.Min(city.yMax,region.yMax));
                if(current!=e.sector)
                {
                    current=e.sector;planner.gradingSurface=e.sector=="Ankara"&&s.districtGrading?
                        new GradingSurface(l,city,s.grade):null;
                }
                var old=e.points;float oldCost=planner.QualityPathCost(old);
                index.Remove(e);
                try
                {
                    if(planner.TryStreet(e.a,e.b,bounds,e.kind,index,false,out List<Vector3> replacement)&&
                        planner.QualityPathCost(replacement)+.05f<oldCost)
                    {e.points=replacement;improved++;}
                }
                finally{index.Add(e);}
            }
            clone.version=source.version+" + regional refinement";
            clone.earthworks.Clear();clone.gradingZones.Clear();clone.gradingMarkers.Clear();clone.parcels.Clear();
            clone.notes.Clear();clone.warnings.Clear();
            planner.BuildEarthworkLines();planner.BuildGradingZones();planner.BuildParcels();planner.EvaluateOpenSpaceAccess();
            planner.ReportGrading("Ankara");planner.ValidateUrbanPlan();
            if(source.version.StartsWith("V7",StringComparison.Ordinal))planner.ValidateQualityGeometry();
            clone.quality=Measure(clone,s);
            var previousQuality=source.quality??Measure(source,s);
            if(clone.quality.peak>previousQuality.peak+.25f||clone.quality.extraFraction>previousQuality.extraFraction+.005f||
                clone.quality.meanCut+clone.quality.meanFill>previousQuality.meanCut+previousQuality.meanFill+.05f)
                throw new InvalidOperationException("Regional candidate increased sampled grading cost. Previous preview retained.");
            AddQualityNotes(clone);
            if(clone.detachedEdges.Count>0)clone.warnings.Add(clone.detachedEdges.Count+" detached diagnostics remain from source plan.");
            if(!clone.quality.bridgeGraphConnected)clone.warnings.Add("Requested bridge graph is incomplete.");
            if(clone.accessResults.Any(a=>!a.connected))clone.warnings.Add("Some manual anchors remain unresolved.");
            clone.notes.Insert(0,"Regional refinement: "+improved+"/"+targets.Count+" eligible segments improved. Fixed endpoints and topology; exterior road geometry, bridges, and authored links unchanged. Parcel/access overlays refreshed globally.");
            return clone;
        }
    }
}
