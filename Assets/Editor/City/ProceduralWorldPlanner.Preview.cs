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
            public List<AnalysisLine> hotspots,verticalFlags;
            public List<VirtualSection> virtualSections;
            public VirtualTerrainReport virtualReport;
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
                writer.Write("delivery-driver-procedural-preview-v7.2-local-v7");
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
                hotspots=p.hotspots.ToList(),verticalFlags=p.verticalFlags.ToList(),virtualSections=p.virtualSections.ToList(),virtualReport=p.virtualReport,
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
            result.hotspots.AddRange(saved.hotspots??new List<AnalysisLine>());result.verticalFlags.AddRange(saved.verticalFlags??new List<AnalysisLine>());
            result.virtualSections.AddRange(saved.virtualSections??new List<VirtualSection>());result.virtualReport=saved.virtualReport;
            if(result.virtualSections.Any(x=>x==null||x.original==null||x.proposed==null||x.original.Length!=7||x.proposed.Length!=7||
                x.original.Any(v=>!Finite(v))||x.proposed.Any(v=>!Finite(v))))throw new InvalidOperationException("Invalid virtual terrain section.");
            var diagnostic=result.virtualReport;
            if(diagnostic!=null&&diagnostic.diagnosticSchema!=0)
            {
                if(diagnostic.diagnosticSchema!=1||diagnostic.issues==null||diagnostic.issues.Count>VirtualIssueLimit||
                    diagnostic.issues.Any(x=>x==null||!Finite(x.position)||x.samples==null||x.samples.Count>VirtualCellSampleLimit||
                        x.samples.Any(v=>v==null||!Finite(v.position)||!Finite(v.center))))
                    throw new InvalidOperationException("Invalid virtual diagnostic evidence.");
                if(diagnostic.diagnosticCells>0||diagnostic.issueCount>0||diagnostic.omittedIssues>0)
                    result.previewCriteriaMet=false;
            }
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

        // V7.3 pilot preflight. Native heightmap window, no TerrainData writes.
        // This is deliberately a bounded, local preview, not a generation permission.
        [Serializable] internal sealed class PilotSurface
        {
            public string city,fingerprint,scenePath;
            public Rect region;
            public int row,column,nx,nz,roadCount,wholeRoads,connectedWholeRoads,lotCount,lotFailures,clearanceSamples,buriedSamples;
            public int urbanGroundCells,steepShoulderPairs;
            public int geometryRoads,junctionFlags,knownIssues,excludedCorridors,protectedCells,overlapCells,changedCells;
            public float originX,originZ,stepX,stepZ,maxCut,maxFill,maxPenetration,maxCoreDelta;
            public float[] original,proposed;
            public Vector3 worstPoint;
            public bool complete,localPass;
            public string limitation;
            public bool Pass=>complete&&roadCount>0&&connectedWholeRoads>=2&&lotCount>0&&lotFailures==0&&buriedSamples==0&&
                geometryRoads==0&&junctionFlags==0&&knownIssues==0&&excludedCorridors==0&&protectedCells==0&&overlapCells==0&&
                urbanGroundCells==0&&steepShoulderPairs==0;
            public string Summary()=>"V7.3 pilot "+city+" tile ["+row+","+column+"] "+region.ToString("F1")+
                ": "+(Pass?"LOCAL PREFLIGHT PASS":"BLOCKED")+"; native grid "+nx+"x"+nz+" at "+stepX.ToString("F2")+"/"+stepZ.ToString("F2")+
                " m; roads touching / wholly inside / largest connected whole-road group "+roadCount+"/"+wholeRoads+"/"+connectedWholeRoads+", existing matched lots "+lotCount+", lot relief failures "+lotFailures+
                "; clearance samples / buried "+clearanceSamples+"/"+buriedSamples+", max penetration "+maxPenetration.ToString("F3")+" m; "+
                "geometry roads / junction flags "+geometryRoads+"/"+junctionFlags+", known virtual issues "+knownIssues+
                ", excluded corridors "+excludedCorridors+", protected demand / core overlap cells "+protectedCells+"/"+overlapCells+
                "; steep shoulder neighbor pairs "+steepShoulderPairs+", urban ground adjustment cells "+urbanGroundCells+
                "; changed native samples "+changedCells+", max cut/fill "+maxCut.ToString("F2")+"/"+maxFill.ToString("F2")+
                " m; max core target spread "+maxCoreDelta.ToString("F2")+" m. "+(limitation??"")+
                " Read-only local preflight, not mesh, collider, driveway or boundary-transition validation; Generate remains locked.";
        }
        private sealed class PilotChoice
        {
            public Tile tile;
            public Rect region;
            public int lots;
        }
        private static bool PilotContains(Rect r,Rect b)=>Contains(r,b.min)&&Contains(r,b.max);
        private static Rect PilotExpand(Rect r,float d)=>Rect.MinMaxRect(r.xMin-d,r.yMin-d,r.xMax+d,r.yMax+d);
        private static bool PilotSegmentNear(Rect r,Vector3 a,Vector3 b,float d)
        {
            // Bounding-box screen deliberately errs on the conservative side.
            return Mathf.Max(a.x,b.x)+d>=r.xMin&&Mathf.Min(a.x,b.x)-d<=r.xMax&&
                Mathf.Max(a.z,b.z)+d>=r.yMin&&Mathf.Min(a.z,b.z)-d<=r.yMax;
        }
        internal static float PilotStampHeight(float raw,float target,float distance,float core,float blend,float cutFill,float baseY,float range)
        {
            float factor=distance<=core?1f:1f-Mathf.SmoothStep(0f,1f,(distance-core)/Mathf.Max(.001f,blend));
            return baseY+Mathf.Clamp01((raw+Mathf.Clamp(target-raw,-cutFill,cutFill)*factor-baseY)/range)*range;
        }
        internal static bool PilotHeight(PilotSurface p,Vector2 pos,out float h)
        {
            h=0;
            if(p==null||p.proposed==null||p.nx<2||p.nz<2||p.stepX<=0||p.stepZ<=0||(long)p.nx*p.nz!=p.proposed.Length)return false;
            float x=(pos.x-p.originX)/p.stepX,z=(pos.y-p.originZ)/p.stepZ;
            if(x<-.001f||z<-.001f||x>p.nx-1+.001f||z>p.nz-1+.001f)return false;
            x=Mathf.Clamp(x,0,p.nx-1);z=Mathf.Clamp(z,0,p.nz-1);
            int ix=Mathf.Min(p.nx-2,Mathf.FloorToInt(x)),iz=Mathf.Min(p.nz-2,Mathf.FloorToInt(z));
            float a=Mathf.Lerp(p.proposed[iz*p.nx+ix],p.proposed[iz*p.nx+ix+1],x-ix);
            float b=Mathf.Lerp(p.proposed[(iz+1)*p.nx+ix],p.proposed[(iz+1)*p.nx+ix+1],x-ix);
            h=Mathf.Lerp(a,b,z-iz);return true;
        }
        internal static PilotSurface FindPilotSurface(Settings s,Layout l,Scene scene,Plan source,string city,string fingerprint)
        {
            if(source==null||source.quality==null||source.virtualReport==null||source.virtualReport.diagnosticSchema!=1||
                source.virtualReport.issues==null||source.virtualReport.omittedIssues>0||source.virtualReport.overflowCells>0)
                throw new InvalidOperationException("Complete final virtual diagnostics are required for pilot preflight.");
            var planner=new ProceduralWorldPlanner(s,l,scene,source);
            Rect cityBounds=city=="Ankara"?l.Ankara(s.ankaraLow):l.Istanbul(s.ankaraLow);
            var lots=source.parcels.Where(x=>x.sector==city&&x.accessible&&!string.IsNullOrEmpty(x.prefab)).ToList();
            var choices=new List<PilotChoice>();
            // Fixed 250m neighborhood squares on land tile interiors; keep a
            // halo for stamp influences, native interpolation and road-edge probes.
            const float size=250f;
            foreach(var tile in l.tiles.Where(x=>x.row!=3&&cityBounds.Contains(x.area.center)).OrderBy(x=>x.row).ThenBy(x=>x.column))
            {
                float dx=tile.area.width/(tile.terrain.terrainData.heightmapResolution-1);
                float dz=tile.area.height/(tile.terrain.terrainData.heightmapResolution-1);
                float halo=s.width*.5f+s.sidewalk+s.blend+2f*Mathf.Sqrt(dx*dx+dz*dz)+2f;
                for(float x=tile.area.xMin+halo;x+size<=tile.area.xMax-halo;x+=size*.5f)
                for(float z=tile.area.yMin+halo;z+size<=tile.area.yMax-halo;z+=size*.5f)
                {
                    var r=new Rect(x,z,size,size);
                    int count=lots.Count(a=>PilotContains(r,a.bounds));
                    if(count>0)choices.Add(new PilotChoice{tile=tile,region=r,lots=count});
                }
            }
            if(choices.Count==0)throw new InvalidOperationException("No 250m interior pilot square with a measured prefab-fit lot found.");
            // Prefer regions with no already-known virtual issue or protected
            // area, then more lots. This is a pilot selection, not city clearance.
            var shortlist=choices.OrderBy(c=>source.virtualReport.issues.Count(i=>Contains(c.region,XZ(i.position),s.width*.5f+s.sidewalk+s.blend)))
                .ThenBy(c=>source.protectedAreas.Count(r=>r.Overlaps(c.region))).ThenByDescending(c=>c.lots)
                .ThenBy(c=>c.tile.row).ThenBy(c=>c.tile.column).ThenBy(c=>c.region.xMin).ThenBy(c=>c.region.yMin).Take(8).ToList();
            PilotSurface best=null;
            for(int i=0;i<shortlist.Count;i++)
            {
                if(EditorUtility.DisplayCancelableProgressBar("V7.3 pilot surface","Neighborhood "+(i+1)+"/"+shortlist.Count,.1f+.8f*i/shortlist.Count))
                    throw new OperationCanceledException();
                var candidate=planner.BuildPilotSurface(shortlist[i],city,fingerprint,scene.path);
                if(candidate.Pass)return candidate;
                if(best==null||PilotBlockers(candidate)<PilotBlockers(best))best=candidate;
            }
            best.limitation=(best.limitation??"")+" No passing square among "+shortlist.Count+" shortlisted candidates; showing least-blocked sampled candidate.";
            return best;
        }
        private static long PilotBlockers(PilotSurface p)=>!p.complete?long.MaxValue:
            (long)p.buriedSamples+p.geometryRoads+p.junctionFlags+p.knownIssues+p.excludedCorridors+p.protectedCells+
            p.overlapCells+p.lotFailures+p.urbanGroundCells+p.steepShoulderPairs+(p.connectedWholeRoads<2?1:0)+(p.roadCount==0?1:0)+(p.lotCount==0?1:0);
        internal static int LargestPilotRoadGroup(List<Edge> roads)=>PilotRoadGroup(roads).Count;
        internal static List<Edge> PilotRoadGroup(List<Edge> roads)
        {
            var adjacency=roads.SelectMany(e=>new[]{(node:e.a,edge:e),(node:e.b,edge:e)})
                .GroupBy(x=>x.node).ToDictionary(g=>g.Key,g=>g.Select(x=>x.edge).ToList());
            var seen=new HashSet<Edge>();var largest=new List<Edge>();
            foreach(var e in roads)
            {
                if(!seen.Add(e))continue;
                var group=new List<Edge>();var pending=new Queue<Edge>();pending.Enqueue(e);
                while(pending.Count>0)
                {
                    var current=pending.Dequeue();group.Add(current);
                    foreach(var node in new[]{current.a,current.b})
                        foreach(var next in adjacency[node])if(seen.Add(next))pending.Enqueue(next);
                }
                if(group.Count>largest.Count)largest=group;
            }
            var selected=new HashSet<Edge>(largest);
            return roads.Where(selected.Contains).ToList();
        }
        internal static List<Edge> PhysicalPilotRoads(Plan source,PilotSurface pilot,Settings s)
        {
            float half=s.width*.5f+s.sidewalk;
            var whole=source.edges.Where(e=>!e.Bridge&&e.sector==pilot.city&&e.points.All(v=>Contains(pilot.region,XZ(v),-half))).ToList();
            var group=PilotRoadGroup(whole);
            if(whole.Count!=pilot.wholeRoads||group.Count!=pilot.connectedWholeRoads||group.Count<2)
                throw new InvalidOperationException("Pilot road scope no longer matches the verified surface.");
            return group;
        }
        private PilotSurface BuildPilotSurface(PilotChoice choice,string city,string fingerprint,string scenePath)
        {
            var tile=choice.tile;var data=tile.terrain.terrainData;var region=choice.region;
            int res=data.heightmapResolution;
            float dx=tile.area.width/(res-1),dz=tile.area.height/(res-1),diag=Mathf.Sqrt(dx*dx+dz*dz);
            float half=settings.width*.5f+settings.sidewalk,core=half+diag,radius=core+settings.blend;
            float halo=radius+diag+2f;
            var p=new PilotSurface{city=city,fingerprint=fingerprint,scenePath=scenePath,region=region,row=tile.row,column=tile.column,stepX=dx,stepZ=dz};
            Rect window=PilotExpand(region,halo);
            int x0=Mathf.Clamp(Mathf.FloorToInt((window.xMin-tile.area.xMin)/dx),0,res-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((window.yMin-tile.area.yMin)/dz),0,res-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((window.xMax-tile.area.xMin)/dx),0,res-1);
            int z1=Mathf.Clamp(Mathf.CeilToInt((window.yMax-tile.area.yMin)/dz),0,res-1);
            p.originX=tile.area.xMin+x0*dx;p.originZ=tile.area.yMin+z0*dz;p.nx=x1-x0+1;p.nz=z1-z0+1;
            long size=(long)p.nx*p.nz;
            if(size>250000||p.nx<2||p.nz<2||!PilotContains(tile.area,window)||data.size.y<=0)
            {p.limitation="Native sample budget or tile halo unavailable.";return p;}
            int count=(int)size;
            float baseY=tile.terrain.transform.position.y;
            var raw=data.GetHeights(x0,z0,p.nx,p.nz);
            p.original=new float[count];p.proposed=new float[count];
            var best=new float[count];var target=new float[count];var minCore=new float[count];var maxCore=new float[count];
            var coreOwner=new Edge[count];var distinctCore=new bool[count];
            for(int z=0;z<p.nz;z++)for(int x=0;x<p.nx;x++)
            {
                int k=z*p.nx+x;p.original[k]=p.proposed[k]=baseY+raw[z,x]*data.size.y;
                best[k]=minCore[k]=float.PositiveInfinity;maxCore[k]=float.NegativeInfinity;
            }
            bool Touches(Edge e,Rect r,float d)
            {
                for(int i=1;i<e.points.Count;i++)if(PilotSegmentNear(r,e.points[i-1],e.points[i],d))return true;
                return false;
            }
            var roads=plan.edges.Where(e=>!e.Bridge&&Touches(e,window,radius)).ToList();
            var touched=roads.Where(e=>Touches(e,region,half)).ToList();
            p.roadCount=touched.Count;
            var whole=touched.Where(e=>e.points.All(v=>Contains(region,XZ(v),-half))).ToList();
            p.wholeRoads=whole.Count;
            p.connectedWholeRoads=LargestPilotRoadGroup(whole);
            p.excludedCorridors=plan.edges.Where(e=>e.Bridge).Concat(plan.authoredLinks).Concat(plan.detachedEdges)
                .Count(e=>Touches(e,window,radius));
            p.knownIssues=plan.virtualReport.issues.Count(i=>Contains(window,XZ(i.position))||
                i.samples.Any(v=>Contains(window,XZ(v.position))||Contains(window,XZ(v.center))));
            float limit=1f/Mathf.Max(1f,settings.verticalRadius);
            p.geometryRoads=touched.Count(e=>!RadiusOk(e.points,e.kind)||VerticalStats(e.points,limit).count>0||
                e.points.Skip(1).Select((v,i)=>GradeOk(e.points[i],v)).Any(ok=>!ok));
            p.junctionFlags=JunctionIncident(plan).Where(x=>Contains(window,XZ(x.Key.position))).Sum(x=>JunctionStats(x.Key,x.Value,limit).count);
            // Match the existing nonbridge stamp algorithm and native grid
            // ordering. Track core target spread separately; nearest owner is
            // not treated as a resolution of incompatible requests.
            float r2=radius*radius,c2=core*core;
            foreach(var e in roads)
            for(int i=1;i<e.points.Count;i++)
            {
                Vector3 va=e.points[i-1],vb=e.points[i];
                if(!PilotSegmentNear(window,va,vb,radius))continue;
                Vector2 a=XZ(va),b=XZ(vb),d=b-a;
                int ax=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-p.originX)/dx),0,p.nx-1);
                int bx=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+radius-p.originX)/dx),0,p.nx-1);
                int az=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius-p.originZ)/dz),0,p.nz-1);
                int bz=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y)+radius-p.originZ)/dz),0,p.nz-1);
                for(int z=az;z<=bz;z++)for(int x=ax;x<=bx;x++)
                {
                    Vector2 q=new Vector2(p.originX+x*dx,p.originZ+z*dz);
                    float t=d.sqrMagnitude>.00001f?Mathf.Clamp01(Vector2.Dot(q-a,d)/d.sqrMagnitude):0f;
                    float dist=(q-(a+t*d)).sqrMagnitude;if(dist>r2)continue;
                    int k=z*p.nx+x;float y=Mathf.Lerp(va.y,vb.y,t)-.20f;
                    if(dist<=c2)
                    {
                        if(coreOwner[k]==null)coreOwner[k]=e;
                        else if(coreOwner[k]!=e)distinctCore[k]=true;
                        minCore[k]=Mathf.Min(minCore[k],y);maxCore[k]=Mathf.Max(maxCore[k],y);
                    }
                    if(dist<best[k]){best[k]=dist;target[k]=y;}
                }
            }
            var urban=plan.parcels.Concat(plan.openSpaces).Where(a=>a.bounds.Overlaps(window)).ToList();
            for(int z=0;z<p.nz;z++)for(int x=0;x<p.nx;x++)
            {
                int k=z*p.nx+x;if(float.IsInfinity(best[k]))continue;
                Vector2 q=new Vector2(p.originX+x*dx,p.originZ+z*dz);
                if(distinctCore[k])
                {
                    float spread=maxCore[k]-minCore[k];p.maxCoreDelta=Mathf.Max(p.maxCoreDelta,spread);
                    if(spread>.5f)p.overlapCells++;
                }
                float y=PilotStampHeight(p.original[k],target[k],Mathf.Sqrt(best[k]),core,settings.blend,settings.maxCutFill,baseY,data.size.y);
                if(IsProtected(q,0f)||obstacles.Near(q,half+1f))
                {
                    if(Mathf.Abs(y-p.original[k])>.0001f||best[k]<=c2)p.protectedCells++;
                    continue;
                }
                p.proposed[k]=y;float shift=y-p.original[k];
                if(Mathf.Abs(shift)>.0001f)p.changedCells++;
                if(Mathf.Abs(shift)>.5f&&urban.Any(a=>Contains(a.bounds,q)))p.urbanGroundCells++;
                p.maxCut=Mathf.Max(p.maxCut,-shift);p.maxFill=Mathf.Max(p.maxFill,shift);
            }
            // Sample gradients between neighboring native nodes at the shoulder
            // of the simulated corridor, including its transition to untouched land.
            bool Shoulder(int k)=>best[k]>c2&&best[k]<=r2;
            for(int z=0;z<p.nz;z++)for(int x=0;x<p.nx;x++)
            {
                int k=z*p.nx+x;
                if(x+1<p.nx&&(Shoulder(k)||Shoulder(k+1))&&
                    Mathf.Abs(p.proposed[k+1]-p.proposed[k])/dx>settings.shoulderSlopeLimit)p.steepShoulderPairs++;
                if(z+1<p.nz&&(Shoulder(k)||Shoulder(k+p.nx))&&
                    Mathf.Abs(p.proposed[k+p.nx]-p.proposed[k])/dz>settings.shoulderSlopeLimit)p.steepShoulderPairs++;
            }
            // Same 1m center/asphalt edge stations as physical terrain Validate,
            // plus sidewalk edges. Bilinear interpolation is a preflight proxy.
            foreach(var e in roads)
            for(int i=1;i<e.points.Count;i++)
            {
                Vector3 a=e.points[i-1],b=e.points[i];Vector2 d=XZ(b-a);
                if(d.sqrMagnitude<.000001f||!PilotSegmentNear(region,a,b,half))continue;
                var right=new Vector2(-d.y,d.x).normalized;
                int stations=Mathf.Max(1,Mathf.CeilToInt(d.magnitude));
                foreach(float offset in new[]{-half,-settings.width*.5f,0f,settings.width*.5f,half})
                for(int j=0;j<=stations;j++)
                {
                    Vector3 center=Vector3.Lerp(a,b,j/(float)stations);Vector2 q=XZ(center)+right*offset;
                    if(!Contains(region,q))continue;
                    p.clearanceSamples++;
                    if(!PilotHeight(p,q,out float h)){p.buriedSamples++;continue;}
                    float penetration=h-(center.y+.03f);
                    if(penetration>.08f)
                    {
                        p.buriedSamples++;
                        if(penetration>p.maxPenetration){p.maxPenetration=penetration;p.worstPoint=XYZ(q,center.y);}
                    }
                }
            }
            foreach(var lot in plan.parcels.Where(x=>x.sector==city&&x.accessible&&!string.IsNullOrEmpty(x.prefab)&&PilotContains(region,x.bounds)))
            {
                p.lotCount++;
                var corners=lot.corners.Select(XZ).ToArray();
                var points=corners.Concat(Enumerable.Range(0,4).Select(i=>(corners[i]+corners[(i+1)%4])*.5f))
                    .Concat(new[]{(corners[0]+corners[2])*.5f});
                float lo=float.PositiveInfinity,hi=float.NegativeInfinity;bool ok=true;
                foreach(var q in points)
                {
                    if(!PilotHeight(p,q,out float y)){ok=false;break;}
                    lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);
                }
                if(!ok||hi-lo>3f)p.lotFailures++;
            }
            p.complete=p.clearanceSamples>0;
            p.localPass=p.Pass;
            p.limitation="250m interior window with external road influence halo; no physical road truncation or boundary splice planned.";
            return p;
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
            if(source.version.StartsWith("V7.1",StringComparison.Ordinal)||source.version.StartsWith("V7.2",StringComparison.Ordinal))planner.ValidateStageGrades();
            else if(source.version.StartsWith("V7",StringComparison.Ordinal))planner.ValidateQualityGeometry();
            clone.quality=Measure(clone,s);
            var previousQuality=source.quality??Measure(source,s);
            if(clone.quality.peak>previousQuality.peak+.25f||clone.quality.extraFraction>previousQuality.extraFraction+.005f||
                clone.quality.meanCut+clone.quality.meanFill>previousQuality.meanCut+previousQuality.meanFill+.05f)
                throw new InvalidOperationException("Regional candidate increased sampled grading cost. Previous preview retained.");
            if(source.version.StartsWith("V7.2",StringComparison.Ordinal))
            {
                if(!NoTerrainRegression(previousQuality,clone.quality,s)||clone.quality.sharpRoads>previousQuality.sharpRoads||
                    clone.quality.verticalSeverity>previousQuality.verticalSeverity+.0001f||clone.quality.junctionGradeFlags>previousQuality.junctionGradeFlags)
                    throw new InvalidOperationException("Regional candidate regressed V7.2 quality; previous preview retained.");
                planner.FindAlternateParkAccess();clone.quality=Measure(clone,s);
                planner.BuildLocalDiagnostics();planner.BuildVirtualTerrain();clone.previewCriteriaMet=false;
            }
            AddQualityNotes(clone);
            if(clone.detachedEdges.Count>0)clone.warnings.Add(clone.detachedEdges.Count+" detached diagnostics remain from source plan.");
            if(!clone.quality.bridgeGraphConnected)clone.warnings.Add("Requested bridge graph is incomplete.");
            if(clone.accessResults.Any(a=>!a.connected))clone.warnings.Add("Some manual anchors remain unresolved.");
            clone.notes.Insert(0,"Regional refinement: "+improved+"/"+targets.Count+" eligible segments improved. Fixed endpoints and topology; exterior road geometry, bridges, and authored links unchanged. Parcel/access overlays refreshed globally.");
            return clone;
        }
    }
}
