using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeliveryDriver.City;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public sealed partial class ProceduralWorldGeneratorWindow
    {
        private string previewFingerprint,previewSettings,analysisCacheKey,previewScenePath;
        private ProceduralWorldPlanner.Plan analysisCache;
        private readonly Dictionary<string,ProceduralWorldPlanner.Plan> planCache=new Dictionary<string,ProceduralWorldPlanner.Plan>();
        private readonly List<string> cacheOrder=new List<string>();
        private bool qualitySettingsOpen=true,qualityWeightsOpen=false,storageOpen=true,cacheEnabled=true;
        private NeighborhoodZone refineZone;
        private ProceduralWorldPlanner.Plan comparisonOwner;
        private int stageChoice;
        private bool autoCheckpoints=true;
        private int refineTile;

        private void OnPreviewSceneChanged(Scene oldScene,Scene newScene)
        {
            comparisonOwner=null;preview=null;layout=null;previewFingerprint=null;analysisCache=null;analysisCacheKey=null;
            planCache.Clear();cacheOrder.Clear();settings.accessAnchors.Clear();anchorSamples=null;pickAnchor=false;
            refineZone=null;pilotSurface=null;pilotOwner=null;Repaint();SceneView.RepaintAll();
        }
        private bool TryCachedPlan(string key,out ProceduralWorldPlanner.Plan p)
        {
            p=null;
            if(!cacheEnabled||!planCache.TryGetValue(key,out p))return false;
            cacheOrder.Remove(key);cacheOrder.Add(key);return true;
        }
        private void CachePlan(string key,ProceduralWorldPlanner.Plan p)
        {
            if(!cacheEnabled)return;
            planCache[key]=p;cacheOrder.Remove(key);cacheOrder.Add(key);
            while(cacheOrder.Count>3){planCache.Remove(cacheOrder[0]);cacheOrder.RemoveAt(0);}
        }
        private void DrawQualityControls()
        {
            qualitySettingsOpen=EditorGUILayout.Foldout(qualitySettingsOpen,"V7.2 Urban Quality",true);
            if(!qualitySettingsOpen)return;
            settings.qualityDesign=EditorGUILayout.Toggle("Enable V7 quality design",settings.qualityDesign);
            using(new EditorGUI.DisabledScope(!settings.qualityDesign))
            {
                settings.stagedOptimization=EditorGUILayout.Toggle("V7.1 staged optimization",settings.stagedOptimization);
                using(new EditorGUI.DisabledScope(settings.stagedOptimization))
                    settings.qualityGuard=EditorGUILayout.Toggle("Preserve V6 quality (legacy V7)",settings.qualityGuard);
                if(settings.stagedOptimization)
                {
                    settings.profileSweeps=EditorGUILayout.IntSlider("Profile optimization sweeps",settings.profileSweeps,4,40);
                    settings.localOptimization=EditorGUILayout.Toggle("V7.2 local optimization",settings.localOptimization);
                    if(settings.localOptimization)
                    {
                        settings.localRepairBudget=EditorGUILayout.IntSlider("Local repair attempt budget",settings.localRepairBudget,40,360);
                        settings.verticalRadius=EditorGUILayout.Slider("Vertical curve radius (m)",settings.verticalRadius,30f,200f);
                        settings.relocateBridgeGates=EditorGUILayout.Toggle("Try alternative bridge gates",settings.relocateBridgeGates);
                        settings.virtualSectionSpacing=EditorGUILayout.Slider("Virtual section spacing (m)",settings.virtualSectionSpacing,10f,50f);
                        settings.shoulderSlopeLimit=EditorGUILayout.Slider("Shoulder slope limit",settings.shoulderSlopeLimit,.25f,1f);
                    }
                    EditorGUILayout.HelpBox("V6 is the starting graph. Each candidate is checked independently; coverage, existing access, grade and bridge connectivity cannot regress. Existing radius defects remain diagnostic until repaired.",MessageType.Info);
                }
                settings.preferredGrading=EditorGUILayout.Slider("Preferred grading cap (m)",settings.preferredGrading,Mathf.Max(10f,settings.maxCutFill),30f);
                settings.minTurnRadius=EditorGUILayout.Slider("Local turn radius (m)",settings.minTurnRadius,8f,45f);
                settings.minLocalStub=EditorGUILayout.Slider("Short local stub (m)",settings.minLocalStub,20f,90f);
                settings.arterialLengthTarget=EditorGUILayout.Slider("Arterial length target (%)",settings.arterialLengthTarget*100f,5f,40f)/100f;
                settings.collectorLengthTarget=EditorGUILayout.Slider("Collector length target (%)",settings.collectorLengthTarget*100f,10f,45f)/100f;
                settings.parcelAccessGrade=EditorGUILayout.Slider("Access slope limit (%)",settings.parcelAccessGrade*100f,5f,25f)/100f;
                EditorGUILayout.HelpBox("V7 prefers lower grading caps but never exceeds the configured hard district/road limits. Radius applies to sampled centerlines (collector x1.2, arterial x1.5), not the final spline vehicle test. V6 guard rejects selected regressions; V6 fallback is explicitly marked.",MessageType.Info);
                qualityWeightsOpen=EditorGUILayout.Foldout(qualityWeightsOpen,"Quality evaluator weights",true);
                if(qualityWeightsOpen)
                {
                    settings.connectivityWeight=EditorGUILayout.IntSlider("Connectivity",settings.connectivityWeight,0,50);
                    settings.terrainWeight=EditorGUILayout.IntSlider("Terrain fit",settings.terrainWeight,0,50);
                    settings.geometryWeight=EditorGUILayout.IntSlider("Geometry",settings.geometryWeight,0,50);
                    settings.parcelWeight=EditorGUILayout.IntSlider("Prefab / parcel fit",settings.parcelWeight,0,50);
                    settings.bridgeWeight=EditorGUILayout.IntSlider("Bridge separation",settings.bridgeWeight,0,50);
                    settings.parkWeight=EditorGUILayout.IntSlider("Park access",settings.parkWeight,0,50);
                }
            }
        }
        private void DrawQualityReport()
        {
            if(preview.quality==null)return;
            var q=preview.quality;
            EditorGUILayout.LabelField("Urban quality score",q.score.ToString("F1")+"/100");
            if(preview.version.StartsWith("V7.1")||preview.version.StartsWith("V7.2"))EditorGUILayout.HelpBox(preview.previewCriteriaMet?"Mandatory preview checks passed; bake remains locked.":"Mandatory preview checks NOT met. Score does not override them.",preview.previewCriteriaMet?MessageType.Info:MessageType.Warning);
            EditorGUILayout.LabelField("Connectivity / terrain / geometry",q.connectivity.ToString("F0")+" / "+q.terrainFit.ToString("F0")+" / "+q.geometry.ToString("F0"));
            EditorGUILayout.LabelField("Prefab fit / bridge / park",q.parcels.ToString("F0")+" / "+q.bridges.ToString("F0")+" / "+q.parks.ToString("F0"));
            EditorGUILayout.LabelField("Ankara / Istanbul coverage",q.ankaraTiles+"/12 / "+q.istanbulTiles+"/12");
            EditorGUILayout.LabelField("Ankara mean cut / fill",q.meanCut.ToString("F2")+" / "+q.meanFill.ToString("F2")+" m");
            EditorGUILayout.LabelField("Peak / extra grading",q.peak.ToString("F1")+" m / "+(q.extraFraction*100f).ToString("F1")+"%");
            EditorGUILayout.LabelField("Arterial / collector / local km",q.arterialKm.ToString("F1")+" / "+q.collectorKm.ToString("F1")+" / "+q.localKm.ToString("F1"));
            EditorGUILayout.LabelField("Sharp streets / short stubs",q.sharpRoads+" / "+q.shortStubs);
            if(preview.version.StartsWith("V7.2"))
            {
                EditorGUILayout.LabelField("Vertical flags / junction pairs",q.verticalDefects+" / "+q.junctionGradeFlags);
                EditorGUILayout.LabelField("Hotspot segments",preview.hotspots.Count.ToString());
                if(preview.virtualReport!=null)
                {
                    var r=preview.virtualReport;
                    EditorGUILayout.LabelField("Virtual sections / missing",r.sections+" / "+r.missingSections);
                    EditorGUILayout.LabelField("Missing shoulder samples",r.missingSamples.ToString());
                    EditorGUILayout.LabelField("Overlap cells / steep sections",r.conflictingCells+" / "+r.steepSections);
                    EditorGUILayout.LabelField("Protected / parcel samples",r.protectedSamples+" / "+r.parcelSamples);
                    DrawSectionInspector();
                    DrawVirtualIssueInspector(r);
                }
            }
            EditorGUILayout.LabelField("Prefab-fit accessible lots",q.matchedLots+"/"+preview.parcels.Count);
            EditorGUILayout.LabelField("Screened park access",q.accessibleParks+"/"+q.totalParks);
            EditorGUILayout.LabelField("Bridge separation",q.bridgeGap.ToString("F0")+" m");
            EditorGUILayout.LabelField("Requested bridge graph",q.bridgeGraphConnected?"Connected / not required":"Incomplete");
            DrawPilotControls();
        }
        private int virtualSectionIndex;
        private void DrawSectionInspector()
        {
            if(preview.virtualSections.Count==0)return;
            if(GUILayout.Button("Focus largest hotspot")&&preview.hotspots.Count>0)
            {
                var hot=preview.hotspots.OrderByDescending(x=>x.value).First();var sv=SceneView.lastActiveSceneView;
                if(sv!=null){sv.pivot=(hot.a+hot.b)*.5f;sv.size=50f;sv.Repaint();}
            }
            virtualSectionIndex=EditorGUILayout.IntSlider("Inspect virtual section",Mathf.Clamp(virtualSectionIndex,0,preview.virtualSections.Count-1),0,preview.virtualSections.Count-1);
            var section=preview.virtualSections[virtualSectionIndex];
            EditorGUILayout.LabelField("Section position",section.proposed[3].ToString("F1"));
            EditorGUILayout.LabelField("Section status",section.flagged?"Check clearance / blend":"Sampled OK");
            for(int i=0;i<section.original.Length;i++)EditorGUILayout.LabelField("Sample "+i+" original / planned Y",section.original[i].y.ToString("F2")+" / "+section.proposed[i].y.ToString("F2"));
            if(GUILayout.Button("Focus selected section"))
            {
                var view=SceneView.lastActiveSceneView;
                if(view!=null){view.pivot=section.proposed[3];view.size=35f;view.Repaint();}
            }
        }
        private int virtualIssueIndex,virtualIssueFilter;
        private static readonly string[] VirtualIssueFilters={"All","Shared junction","Independent roads","Ambiguous / overflow","Missing sections","Missing shoulders","Steep sections","Protected samples","Parcel samples"};
        private bool virtualIssuesOpen,virtualEvidenceOpen;
        private ProceduralWorldPlanner.VirtualIssue selectedVirtualIssue;
        private ProceduralWorldPlanner.VirtualTerrainReport selectedVirtualReport;
        private void DrawVirtualIssueInspector(ProceduralWorldPlanner.VirtualTerrainReport report)
        {
            if(report.diagnosticSchema!=1||report.issues==null)
            {EditorGUILayout.HelpBox("Legacy snapshot: detailed virtual diagnostics require a fresh preview.",MessageType.Info);return;}
            EditorGUILayout.HelpBox(ProceduralWorldPlanner.VirtualDiagnosticSummary(report),MessageType.Info);
            virtualIssuesOpen=EditorGUILayout.Foldout(virtualIssuesOpen,"Inspect virtual terrain issues ("+report.issues.Count+")",true);
            if(!virtualIssuesOpen||report.issues.Count==0)return;
            int filter=EditorGUILayout.Popup("Issue category",virtualIssueFilter,VirtualIssueFilters);
            if(filter!=virtualIssueFilter||selectedVirtualReport!=report){virtualIssueFilter=filter;virtualIssueIndex=0;}
            var issues=filter==0?report.issues:report.issues.Where(x=>(int)x.kind==filter-1).ToList();
            if(issues.Count==0)
            {
                if(selectedVirtualIssue!=null)SceneView.RepaintAll();
                selectedVirtualIssue=null;selectedVirtualReport=report;EditorGUILayout.LabelField("No recorded issues in category.");return;
            }
            virtualIssueIndex=EditorGUILayout.IntSlider("Issue",Mathf.Clamp(virtualIssueIndex,0,issues.Count-1),0,issues.Count-1);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Previous issue"))virtualIssueIndex=Mathf.Max(0,virtualIssueIndex-1);
                if(GUILayout.Button("Next issue"))virtualIssueIndex=Mathf.Min(issues.Count-1,virtualIssueIndex+1);
            }
            var issue=issues[virtualIssueIndex];
            if(selectedVirtualIssue!=issue||selectedVirtualReport!=report)SceneView.RepaintAll();
            selectedVirtualIssue=issue;selectedVirtualReport=report;
            EditorGUILayout.LabelField("Issue type",issue.kind.ToString());
            EditorGUILayout.LabelField("Location",issue.position.ToString("F2"));
            EditorGUILayout.HelpBox(issue.reason,MessageType.Warning);
            if(issue.kind==ProceduralWorldPlanner.VirtualIssueKind.SharedJunction||issue.kind==ProceduralWorldPlanner.VirtualIssueKind.IndependentRoad||
                issue.kind==ProceduralWorldPlanner.VirtualIssueKind.AmbiguousOverlap)
            {
                EditorGUILayout.LabelField("4m cell / legacy",issue.cell+" / "+issue.legacyConflict);
                if(report.topologySchema==1)EditorGUILayout.LabelField("Shared / unrelated node pairs",issue.hasConnectedPair+" / "+issue.hasUnrelatedPair);
                EditorGUILayout.LabelField("Observed max height delta",issue.maxDelta.ToString("F2")+" m");
                EditorGUILayout.LabelField("Closest conflicting XZ samples",issue.minSeparation<0?"Unknown":issue.minSeparation.ToString("F2")+" m");
                if(issue.maxSampleA>=0)EditorGUILayout.LabelField("Max delta sample indices",issue.maxSampleA+" / "+issue.maxSampleB);
                if(issue.closestSampleA>=0)EditorGUILayout.LabelField("Closest pair sample indices",issue.closestSampleA+" / "+issue.closestSampleB);
            }
            if(issue.kind==ProceduralWorldPlanner.VirtualIssueKind.SteepSection)
                EditorGUILayout.LabelField("Left / right shoulder grade",(issue.leftSlope*100f).ToString("F1")+"% / "+(issue.rightSlope*100f).ToString("F1")+"%");
            if(GUILayout.Button("Focus selected virtual issue"))
            {
                terrainLayer=TerrainLayer.VirtualTerrain;showTerrainAnalysis=true;
                var view=SceneView.lastActiveSceneView;
                if(view!=null){view.pivot=issue.position;view.size=35f;view.Repaint();}
            }
            virtualEvidenceOpen=EditorGUILayout.Foldout(virtualEvidenceOpen,"Sample provenance ("+issue.samples.Count+")",true);
            if(virtualEvidenceOpen)
                for(int i=0;i<issue.samples.Count;i++)
                {
                    var sample=issue.samples[i];
                    EditorGUILayout.LabelField("#"+i+" Road "+sample.roadA+" -> "+sample.roadB,ProceduralWorldPlanner.VirtualSampleName(sample.kind));
                    EditorGUILayout.LabelField("Station / XYZ",sample.distanceFromA.ToString("F1")+" m / "+sample.position.ToString("F2"));
                    EditorGUILayout.LabelField("Ground / requested Y",sample.hasGround?sample.ground.ToString("F2")+" / "+sample.position.y.ToString("F2"):"Missing ground; reference Y only");
                    if(!string.IsNullOrEmpty(sample.context))EditorGUILayout.HelpBox(sample.context,MessageType.Info);
                }
        }
        private void DrawPreviewStorage()
        {
            storageOpen=EditorGUILayout.Foldout(storageOpen,"Preview cache, snapshots & district refinement",true);
            if(!storageOpen)return;
            cacheEnabled=EditorGUILayout.Toggle("Reuse verified previews",cacheEnabled);
            autoCheckpoints=EditorGUILayout.Toggle("Auto-save staged checkpoints",autoCheckpoints);
            DrawStageComparison();
            EditorGUILayout.LabelField("Session cache",planCache.Count+"/3 road plans");
            if(GUILayout.Button("Clear preview caches"))
            {planCache.Clear();cacheOrder.Clear();analysisCache=null;analysisCacheKey=null;}
            using(new EditorGUI.DisabledScope(preview==null||analysisOnly))
            {
                if(GUILayout.Button("Save preview JSON"))Run(SavePreview);
                if(GUILayout.Button("Copy preview report"))EditorGUIUtility.systemCopyBuffer=PreviewReportText();
            }
            if(GUILayout.Button("Load preview JSON (read only)"))Run(LoadPreview);
            using(new EditorGUI.DisabledScope(preview==null||analysisOnly||string.IsNullOrEmpty(previewFingerprint)||!settings.qualityDesign))
            {
                refineZone=(NeighborhoodZone)EditorGUILayout.ObjectField("District to refine",refineZone,typeof(NeighborhoodZone),true);
                if(layout!=null&&refineZone==null)
                {
                    var tiles=layout.tiles.Where(t=>t.row!=3).OrderBy(t=>t.row).ThenBy(t=>t.column).ToList();
                    refineTile=Mathf.Clamp(refineTile,0,Mathf.Max(0,tiles.Count-1));
                    refineTile=EditorGUILayout.Popup("Or choose terrain tile",refineTile,tiles.Select(t=>t.terrain.name+" ["+t.row+","+t.column+"]").ToArray());
                }
                if(GUILayout.Button("Refine selected district / tile"))Run(RefinePreview);
            }
            EditorGUILayout.HelpBox("Snapshots include road paths and overlays, never a scene bake. Exact scene/settings fingerprint is required to refine. Local refinement keeps topology and exterior roads fixed; parcel/access overlays are refreshed. Save the preferred layout before experimenting.",MessageType.Info);
        }
        private ProceduralWorldPlanner.PilotSurface pilotSurface;
        private ProceduralWorldPlanner.Plan pilotOwner;
        private bool pilotOpen=true,pilotAnkara,pilotOverlay=true;
        private bool HasPilot=>pilotSurface!=null&&pilotOwner==preview&&previewFingerprint==pilotSurface.fingerprint;
        private void DrawPilotControls()
        {
            if(preview==null||analysisOnly||preview.virtualReport==null)return;
            pilotOpen=EditorGUILayout.Foldout(pilotOpen,"V7.3 Neighborhood pilot (read only)",true);
            if(!pilotOpen)return;
            pilotAnkara=EditorGUILayout.Popup("Pilot city",pilotAnkara?1:0,new[]{"Istanbul","Ankara"})==1;
            using(new EditorGUI.DisabledScope(string.IsNullOrEmpty(previewFingerprint)))
                if(GUILayout.Button("Find and preflight 250m neighborhood"))Run(PreflightPilot);
            if(!HasPilot)return;
            EditorGUILayout.HelpBox(pilotSurface.Summary()+" Results describe the inputs at the last preflight; rerun after any scene/settings edits.",pilotSurface.Pass?MessageType.Info:MessageType.Warning);
            pilotOverlay=EditorGUILayout.Toggle("Show pilot surface overlay",pilotOverlay);
            if(GUILayout.Button("Focus pilot neighborhood"))
            {
                var view=SceneView.lastActiveSceneView;var c=pilotSurface.region.center;
                if(view!=null){view.pivot=new Vector3(c.x,0,c.y);view.size=190f;
                    if(ProceduralWorldPlanner.PilotHeight(pilotSurface,c,out float y))view.pivot=new Vector3(c.x,y,c.y);
                    view.Repaint();}
            }
            if(GUILayout.Button("Save pilot surface JSON"))Run(SavePilotSurface);
        }
        private void PreflightPilot()
        {
            var scene=SceneManager.GetActiveScene();var detected=ProceduralWorldPlanner.DetectLayout(scene);
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,detected,scene);
            if(key!=previewFingerprint||scene.path!=previewScenePath)throw new InvalidOperationException("Preview inputs changed. Recalculate before pilot preflight.");
            var result=ProceduralWorldPlanner.FindPilotSurface(settings,detected,scene,preview,pilotAnkara?"Ankara":"İstanbul",key);
            pilotSurface=result;pilotOwner=preview;pilotOverlay=true;showPreview=true;
            SetStatus(result.Pass?"Local pilot preflight passed; full generation still locked.":"Pilot preflight blocked; inspect report.",result.Pass?MessageType.Info:MessageType.Warning);
            SceneView.RepaintAll();
        }
        private void SavePilotSurface()
        {
            if(!HasPilot)return;
            var scene=SceneManager.GetActiveScene();var detected=ProceduralWorldPlanner.DetectLayout(scene);
            if(scene.path!=pilotSurface.scenePath||ProceduralWorldPlanner.PreviewFingerprint(settings,detected,scene)!=pilotSurface.fingerprint)
                throw new InvalidOperationException("Pilot inputs changed. Rerun preflight before export.");
            string path=EditorUtility.SaveFilePanel("Save read-only pilot surface",Path.Combine(Directory.GetCurrentDirectory(),"Library"),"V73-pilot","json");
            if(!string.IsNullOrEmpty(path))File.WriteAllText(path,JsonUtility.ToJson(pilotSurface,false));
        }
        private void DrawPilotOverlay()
        {
            if(!HasPilot||!pilotOverlay)return;
            var r=pilotSurface.region;
            Vector3 Lift(Vector2 v)
            {
                ProceduralWorldPlanner.PilotHeight(pilotSurface,v,out float h);
                return new Vector3(v.x,h+1f,v.y);
            }
            Handles.color=pilotSurface.Pass?new Color(.2f,1f,.65f,1f):new Color(1f,.45f,.1f,1f);
            var a=Lift(r.min);var b=Lift(new Vector2(r.xMax,r.yMin));var c=Lift(r.max);var d=Lift(new Vector2(r.xMin,r.yMax));
            Handles.DrawAAPolyLine(4f,a,b,c,d,a);
            Handles.Label(Lift(r.center),"V7.3 PILOT — "+(pilotSurface.Pass?"LOCAL PREFLIGHT PASS":"BLOCKED"));
            if(pilotSurface.complete)
            {
                // Decimated bilinear heightfield wire view, not a scene mesh.
                const int divisions=16;
                for(int i=0;i<=divisions;i++)
                {
                    var row=new Vector3[divisions+1];var col=new Vector3[divisions+1];
                    for(int j=0;j<=divisions;j++)
                    {
                        row[j]=Lift(new Vector2(Mathf.Lerp(r.xMin,r.xMax,j/(float)divisions),Mathf.Lerp(r.yMin,r.yMax,i/(float)divisions)));
                        col[j]=Lift(new Vector2(Mathf.Lerp(r.xMin,r.xMax,i/(float)divisions),Mathf.Lerp(r.yMin,r.yMax,j/(float)divisions)));
                    }
                    Handles.DrawAAPolyLine(1f,row);Handles.DrawAAPolyLine(1f,col);
                }
            }
            if(pilotSurface.maxPenetration>0){Handles.color=Color.red;Handles.DrawWireDisc(pilotSurface.worstPoint,Vector3.up,4f);}
        }
        private string PreviewReportText()
        {
            var lines=new List<string>{"Selected layout: "+preview.version,"Retained/detached roads: "+preview.edges.Count+" / "+preview.detachedEdges.Count,
                "Bridges: "+preview.BridgeCount,"Authored proposals: "+preview.authoredLinks.Count,
                "Open spaces / lots: "+preview.openSpaces.Count+" / "+preview.parcels.Count};
            lines.AddRange(preview.stageReports);lines.AddRange(preview.notes);lines.AddRange(preview.warnings.Select(x=>"WARNING: "+x));
            if(HasPilot)lines.Add(pilotSurface.Summary());
            lines.Add("--- Settings at calculation ---");lines.Add(previewSettings??"Unknown");
            return string.Join("\n",lines);
        }
        private void DrawStageComparison()
        {
            if(preview==null||comparisonOwner==null||comparisonOwner.stageViews.Count==0||analysisOnly)return;
            var views=comparisonOwner.stageViews;
            stageChoice=Mathf.Clamp(stageChoice,0,views.Count-1);
            stageChoice=EditorGUILayout.Popup("Stage comparison",stageChoice,views.Select(x=>x.label).ToArray());
            if(GUILayout.Button("View selected stage (read only)"))
            {
                var saved=views[stageChoice].snapshot;
                preview=ProceduralWorldPlanner.Restore(saved,comparisonOwner.layout,settings);
                previewFingerprint=null;previewSettings=saved.settingsDescription;previewScenePath=saved.scenePath;
                SetStatus("Historical stage view; refinement disabled. Return to final plan to continue.");
                SceneView.RepaintAll();
            }
            if(GUILayout.Button("Return to final optimized plan"))
            {
                preview=comparisonOwner;previewFingerprint=finalStageKey;previewSettings=finalStageSettings;
                previewScenePath=finalStageScene;SceneView.RepaintAll();
            }
        }

        private string finalStageKey,finalStageSettings,finalStageScene;
        private void WriteStageCheckpoints(ProceduralWorldPlanner.Plan plan,string key,string scenePath)
        {
            finalStageKey=key;finalStageSettings=ProceduralWorldPlanner.DescribeSettings(settings);finalStageScene=scenePath;
            if(!autoCheckpoints||plan.stageViews.Count==0||string.IsNullOrEmpty(scenePath))return;
            try
            {
                string folder=Path.Combine(Directory.GetCurrentDirectory(),"Library","ProceduralWorldCheckpoints",
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+key.Substring(0,8));
                Directory.CreateDirectory(folder);
                for(int i=0;i<plan.stageViews.Count;i++)
                {
                    var view=plan.stageViews[i];
                    // Intermediate snapshots are diagnostic only, not refinable inputs.
                    File.WriteAllText(Path.Combine(folder,i.ToString("D2")+"-"+(view.accepted?"accepted":"rejected")+".json"),
                        JsonUtility.ToJson(view.snapshot,false));
                }
                File.WriteAllText(Path.Combine(folder,"final.json"),JsonUtility.ToJson(
                    ProceduralWorldPlanner.Capture(plan,key,scenePath,finalStageSettings),false));
                plan.notes.Add("Staged checkpoints saved to "+folder);
            }
            catch(Exception ex){plan.warnings.Add("Checkpoint write failed (preview retained): "+ex.Message);}
        }
        private void SavePreview()
        {
            if(preview==null||analysisOnly)return;
            string path=EditorUtility.SaveFilePanel("Save procedural preview",Path.Combine(Directory.GetCurrentDirectory(),"Library"),"CityPreview-V7","json");
            if(string.IsNullOrEmpty(path))return;
            var scene=SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Save the Unity scene before saving a persistent preview snapshot.");
            var saved=ProceduralWorldPlanner.Capture(preview,previewFingerprint,previewScenePath??scene.path,previewSettings);
            File.WriteAllText(path,JsonUtility.ToJson(saved,false));
            SetStatus("Preview snapshot saved: "+path);
        }
        private void LoadPreview()
        {
            string path=EditorUtility.OpenFilePanel("Load procedural preview",Path.Combine(Directory.GetCurrentDirectory(),"Library"),"json");
            if(string.IsNullOrEmpty(path))return;
            if(new FileInfo(path).Length>150L*1024*1024)throw new InvalidOperationException("Preview file is larger than 150 MB.");
            var saved=JsonUtility.FromJson<ProceduralWorldPlanner.PreviewSnapshot>(File.ReadAllText(path));
            var scene=SceneManager.GetActiveScene();
            if(saved==null||saved.scenePath!=scene.path)throw new InvalidOperationException("Snapshot belongs to a different scene.");
            var detected=ProceduralWorldPlanner.DetectLayout(scene);
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,detected,scene);
            var loaded=ProceduralWorldPlanner.Restore(saved,detected,settings);
            layout=detected;preview=loaded;comparisonOwner=loaded;analysisOnly=false;
            previewFingerprint=key==saved.fingerprint?key:null;previewSettings=saved.settingsDescription;previewScenePath=saved.scenePath;
            if(previewFingerprint==null)
                SetStatus("Loaded view-only snapshot; current terrain, scene inputs or settings differ. Refinement disabled. Saved metrics describe the original plan.",MessageType.Warning);
            else SetStatus("Loaded verified preview snapshot. Current scene and settings match.");
        }
        private void RefinePreview()
        {
            if(preview==null||analysisOnly||string.IsNullOrEmpty(previewFingerprint))return;
            var scene=SceneManager.GetActiveScene();var detected=ProceduralWorldPlanner.DetectLayout(scene);
            string key=ProceduralWorldPlanner.PreviewFingerprint(settings,detected,scene);
            if(key!=previewFingerprint)throw new InvalidOperationException("Inputs changed since preview. Recalculate before refining.");
            Rect region;
            if(refineZone!=null)
            {
                if(refineZone.gameObject.scene!=scene)throw new InvalidOperationException("Choose a district in the active scene.");
                var box=refineZone.GetComponent<BoxCollider>();
                if(box==null)throw new InvalidOperationException("Selected district has no BoxCollider.");
                Bounds b=box.bounds;
                region=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
            }
            else
            {
                var tiles=detected.tiles.Where(t=>t.row!=3).OrderBy(t=>t.row).ThenBy(t=>t.column).ToList();
                region=tiles[Mathf.Clamp(refineTile,0,tiles.Count-1)].area;
            }
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var refined=ProceduralWorldPlanner.RefineRegion(settings,detected,scene,preview,region);
            refined.previewCriteriaMet=false;
            preview=refined;layout=detected;analysisOnly=false;
            SetStatus("District refinement completed in "+timer.Elapsed.TotalSeconds.ToString("F1")+" s. Original full plan remains in session cache.");
        }
    }
}
