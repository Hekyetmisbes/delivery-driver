using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    // Editor-only manual native-wheel sandbox. The saved scene is rendered
    // normally in Scene View; Handles show the actual simulated test body's
    // pose, not a spawned prefab or gameplay camera.
    internal sealed class PilotManualDriveWindow : EditorWindow
    {
        private static PilotManualDriveWindow live;
        internal static bool HasLiveSession=>live!=null&&live.cleanup!=null;
        internal static void FocusLiveSession(){if(live!=null)live.Focus();}

        private Scene source;
        private Rigidbody body;
        private WheelCollider[] wheels;
        private Vector3 boxCenter,boxSize;
        private Vector3[] route;
        private Action<float,float,bool> simulate;
        private Func<string> report;
        private Action cleanup;
        private readonly HashSet<KeyCode> keys=new HashSet<KeyCode>();
        private bool running,follow=true,showRoute=true;
        private double lastTime,accumulator,nextRepaint;
        private string lastReport="",status="No session. Open the pilot manual drive menu.";
        private const float FixedStep=.02f;

        internal static void OpenSession(Scene source,Rigidbody body,WheelCollider[] wheels,Vector3 center,Vector3 size,
            List<Vector3> path,Action<float,float,bool> simulate,Func<string> report,Action cleanup)
        {
            if(HasLiveSession)throw new InvalidOperationException("A manual drive session is already open.");
            var window=GetWindow<PilotManualDriveWindow>("Pilot Manual Drive");
            try
            {
                window.source=source;window.body=body;window.wheels=wheels;window.boxCenter=center;window.boxSize=size;
                window.route=path.ToArray();window.simulate=simulate;window.report=report;window.cleanup=cleanup;
                window.running=false;window.keys.Clear();window.lastTime=EditorApplication.timeSinceStartup;
                window.accumulator=0;window.lastReport=report();window.status="Ready. Click Resume and keep this window focused.";
                window.minSize=new Vector2(330,260);
                live=window;
                EditorApplication.update+=window.UpdateSimulation;
                AssemblyReloadEvents.beforeAssemblyReload+=window.EndBeforeReload;
                EditorApplication.playModeStateChanged+=window.OnPlayState;
                EditorApplication.quitting+=window.EndBeforeReload;
                SceneView.duringSceneGui+=window.DrawScene;
                window.Show();window.Focus();
                var view=SceneView.lastActiveSceneView;
                if(view!=null){view.pivot=body.position;view.size=18f;view.Repaint();}
            }
            catch
            {
                // Caller still owns the preview if setup fails.
                window.Unsubscribe();window.cleanup=null;window.simulate=null;window.report=null;
                if(live==window)live=null;
                throw;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("V7.8 — Manual pilot drive",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Keep a Scene View visible next to this window. The yellow vehicle is the native physics rig's actual box and wheel pose; the city is the existing saved scene. W/up gas, A/D or left/right steering, S/down/Space brake. Forward only; no normal Play Mode.",MessageType.Info);
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            bool active=HasLiveSession&&live==this;
            using(new EditorGUI.DisabledScope(!active))
            {
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button(running?"Pause":"Resume"))
                {
                    if(running)Pause("Paused.");
                    else{keys.Clear();running=true;lastTime=EditorApplication.timeSinceStartup;accumulator=0;status="Driving — keyboard controls active.";Focus();}
                }
                if(GUILayout.Button("End session"))EndSession("Ended by user.");
                EditorGUILayout.EndHorizontal();
                follow=EditorGUILayout.Toggle("Follow vehicle in Scene View",follow);
                showRoute=EditorGUILayout.Toggle("Show verified route",showRoute);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live measurements",EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(lastReport,EditorStyles.wordWrappedLabel,GUILayout.MinHeight(76));
            if(!active)EditorGUILayout.HelpBox("Open Tools > Delivery Driver > City > Open Pilot Manual Drive to start a new rig. The tool does not save the source scene.",MessageType.None);
            var e=Event.current;
            if(active&&running&&(e.type==EventType.KeyDown||e.type==EventType.KeyUp))
            {
                if(e.keyCode==KeyCode.Escape&&e.type==EventType.KeyDown){Pause("Paused with Esc.");e.Use();}
                else if(IsDriveKey(e.keyCode))
                {
                    if(e.type==EventType.KeyDown)keys.Add(e.keyCode);else keys.Remove(e.keyCode);
                    e.Use();
                }
            }
        }
        private static bool IsDriveKey(KeyCode key)=>key==KeyCode.W||key==KeyCode.A||key==KeyCode.S||key==KeyCode.D||
            key==KeyCode.UpArrow||key==KeyCode.DownArrow||key==KeyCode.LeftArrow||key==KeyCode.RightArrow||key==KeyCode.Space;
        private bool Held(KeyCode a,KeyCode b)=>keys.Contains(a)||keys.Contains(b);
        private void Pause(string reason)
        {
            running=false;keys.Clear();accumulator=0;status=reason;Repaint();
        }
        private void OnLostFocus(){if(running)Pause("Paused — window lost keyboard focus. Click Resume.");}
        private void UpdateSimulation()
        {
            if(cleanup==null)return;
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode||!source.IsValid()||
                !source.isLoaded||source.isDirty||SceneManager.sceneCount!=1||SceneManager.GetActiveScene()!=source||body==null)
            {EndSession("Stopped: source scene or editor state changed.");return;}
            double now=EditorApplication.timeSinceStartup,elapsed=Math.Max(0,now-lastTime);lastTime=now;
            if(!running)return;
            if(focusedWindow!=this){Pause("Paused — window lost keyboard focus.");return;}
            // Drop long editor stalls instead of catching up in a large burst.
            accumulator+=Math.Min(elapsed,.1);
            try
            {
                int steps=0;
                while(accumulator>=FixedStep&&steps++<5)
                {
                    accumulator-=FixedStep;
                    float gas=Held(KeyCode.W,KeyCode.UpArrow)?1f:0f;
                    float steer=(Held(KeyCode.D,KeyCode.RightArrow)?1f:0f)-(Held(KeyCode.A,KeyCode.LeftArrow)?1f:0f);
                    bool brake=Held(KeyCode.S,KeyCode.DownArrow)||keys.Contains(KeyCode.Space);
                    simulate(gas,steer,brake);
                }
                if(now>=nextRepaint)
                {
                    nextRepaint=now+.05;lastReport=report();Repaint();
                    var view=SceneView.lastActiveSceneView;
                    if(view!=null&&follow){view.pivot=body.position;view.Repaint();}
                    else SceneView.RepaintAll();
                }
            }
            catch(Exception ex)
            {
                Debug.LogError("[ProceduralWorld] V7.8 MANUAL DRIVE STOPPED: "+ex.Message);
                EndSession("Stopped by physics guard: "+ex.Message);
            }
        }
        private void DrawScene(SceneView view)
        {
            if(cleanup==null||body==null||Event.current.type!=EventType.Repaint)return;
            Color oldColor=Handles.color;Matrix4x4 oldMatrix=Handles.matrix;
            try
            {
                Handles.matrix=Matrix4x4.identity;
                if(showRoute&&route!=null)
                {
                    Handles.color=new Color(.15f,.85f,.45f,.8f);
                    for(int i=0;i<route.Length;i++)
                        Handles.DrawLine(route[i]+Vector3.up*.09f,route[(i+1)%route.Length]+Vector3.up*.09f);
                }
                Handles.matrix=Matrix4x4.TRS(body.position,body.rotation,Vector3.one);
                Handles.color=new Color(1f,.78f,.15f,1f);
                Handles.DrawWireCube(boxCenter,boxSize);
                Vector3 top=boxCenter+Vector3.up*boxSize.y*.5f;
                Vector3 dx=Vector3.right*boxSize.x*.5f,dz=Vector3.forward*boxSize.z*.5f;
                Handles.color=new Color(1f,.78f,.15f,.45f);
                Handles.DrawAAConvexPolygon(top-dx-dz,top+dx-dz,top+dx+dz,top-dx+dz);
                Handles.color=Color.yellow;
                Handles.DrawLine(top,top+Vector3.forward*(boxSize.z*.5f+.75f));
                Handles.matrix=Matrix4x4.identity;
                Handles.color=new Color(.12f,.12f,.12f,1f);
                foreach(var wheel in wheels)
                {
                    if(wheel==null)continue;
                    wheel.GetWorldPose(out Vector3 position,out Quaternion rotation);
                    Handles.DrawWireDisc(position,rotation*Vector3.right,wheel.radius);
                }
                Handles.color=Color.yellow;
                Handles.Label(body.position+Vector3.up*(boxSize.y+1f),"MINIVAN — manual physics");
            }
            finally{Handles.color=oldColor;Handles.matrix=oldMatrix;}
        }
        private void Unsubscribe()
        {
            EditorApplication.update-=UpdateSimulation;
            AssemblyReloadEvents.beforeAssemblyReload-=EndBeforeReload;
            EditorApplication.playModeStateChanged-=OnPlayState;
            EditorApplication.quitting-=EndBeforeReload;
            SceneView.duringSceneGui-=DrawScene;
        }
        private void OnPlayState(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode)EndSession("Ended before Play Mode.");
        }
        private void EndBeforeReload()=>EndSession("Ended before editor reload or shutdown.");
        private void OnDisable()=>EndSession("Manual drive window closed.");
        private void EndSession(string reason)
        {
            Pause(reason);
            Unsubscribe();
            var close=cleanup;cleanup=null;simulate=null;
            if(close!=null)
            {
                try{if(report!=null)lastReport=report();}
                catch(Exception ex){Debug.LogWarning("[ProceduralWorld] Manual drive final telemetry unavailable: "+ex.Message);}
                try{close();}
                catch(Exception ex){status="Cleanup failed: "+ex.Message;Debug.LogException(ex);}
            }
            report=null;body=null;wheels=null;route=null;
            if(live==this)live=null;
            SceneView.RepaintAll();
        }
    }
}
