using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.SceneManagement;using RailDrivingSimulator;
[InitializeOnLoad]public static class Restore104Checks{
 const string Key="Restore104Check";static Restore104Checks(){EditorApplication.update+=Resume;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Main.unity",OpenSceneMode.Single);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 static void Resume(){if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){SessionState.SetBool(Key,false);new GameObject("RecoveryProbe").AddComponent<Restore104Probe>();}}
 public static void Finish(){Debug.Log("RESTORE104_CHECKS_PASS");EditorApplication.Exit(0);}
 public static void Fail(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
}
public class Restore104Probe:MonoBehaviour {
 const BindingFlags B=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(Game g,string n)=>typeof(Game).GetField(n,B).GetValue(g);
 static void Set(Game g,string n,object v)=>typeof(Game).GetField(n,B).SetValue(g,v);
 static void Call(Game g,string n)=>typeof(Game).GetMethod(n,B).Invoke(g,null);
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static void Driving(Game g){Call(g,"DrivingUI");if(!(Get(g,"mobileRoot") is RectTransform r&&r))Call(g,"TouchDrivingUI");}
 IEnumerator Start(){yield return null;Game g=null;try{
 var map=JsonUtility.FromJson<MapManifest>(File.ReadAllText("/Volumes/RailRestoreWork/MapMetadata/manifest.json"));var route=JsonUtility.FromJson<RouteData>(File.ReadAllText("/Volumes/RailRestoreWork/MapMetadata/route.json"));
 var world=new GameObject("PolicyProbe").AddComponent<MapWorld>();world.info=new MapInfo{manifest=map};world.mobileStreaming=true;int samples=0,max=0;
 for(double s=0;s<route.Length;s+=25){var pose=route.Pose(s);var p=new Vector3((float)pose.x,0,(float)pose.z);foreach(float radius in new[]{800f,650f}){world.radius=radius;var chosen=world.Desired(p);Check(chosen.Length<=12,"Unbounded tiles");Check(map.chunks.Where(c=>world.Required(c,p)).All(c=>chosen.Any(x=>x.file==c.file)),"Required tile outside load policy");max=Math.Max(max,chosen.Length);samples++;}}
 UnityEngine.Object.Destroy(world.gameObject);
 g=UnityEngine.Object.FindFirstObjectByType<Game>();Check(g!=null,"Main scene Game missing");var cv=(Canvas)Get(g,"canvas");Check(cv&&cv.GetComponentsInChildren<Button>().Length>=6,"Real startup menu missing");var cab=Resources.Load<GameObject>("Train/E235Cab");Check(cab&&cab.GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh),"Missing cab meshes");IOSLogicTests.Run();RunPlan cp;var compiled=JourneyPlanner.Compile(route,new RunPlan{routeId=route.id,start=0,end=0,selected=Enumerable.Repeat(true,route.stations.Length).ToArray()},out cp);var sim=new Simulation(compiled,cp);Set(g,"sim",sim);Driving(g);
 var root=(RectTransform)Get(g,"mobileRoot");var lo=root.anchorMin;var hi=root.anchorMax;
 for(int i=0;i<4;i++){
  var old=(RectTransform)Get(g,"mobileRoot");Call(g,i%2==0?"Pause":"MobileMore");Check(!old.gameObject.activeSelf,"Old UI still interactive");Driving(g);root=(RectTransform)Get(g,"mobileRoot");Check((root.anchorMin-lo).sqrMagnitude<.000001f&&(root.anchorMax-hi).sqrMagnitude<.000001f,"Safe area lost on return");Check(!(bool)Get(g,"paused"),"Normal driving left paused");
  var slider=(Slider)Get(g,"notchSlider");slider.value=1;Check(sim.state.notch==1,"Slider blocked after return");slider.value=0;Check(sim.state.notch==0,"Slider N blocked");
  var button=root.GetComponentsInChildren<Button>().First(b=>b.gameObject.name=="EB");button.onClick.Invoke();Check(MobileInput.Down("紧急制动"),"EB touch blocked after return");
 }
 string result="RESTORE104_RUNTIME_PASS route_policy_samples="+samples+" maximum_tiles="+max+" required_coverage=true UI_returns=4 safe_area=true slider=true touch=true normal_unpaused=true";File.WriteAllText("/Volumes/KINGSTON/RailSimulator_Restored_1.0.4_20261030/Reports/runtime_checks.txt",result+"\n");Debug.Log(result);
 }catch(Exception e){Restore104Checks.Fail(e);yield break;}
 yield return null;Restore104Checks.Finish();
 }
}
