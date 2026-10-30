using UnityEngine;
namespace RailDrivingSimulator {
 public class SceneryTrain:MonoBehaviour {
  RouteData track;MapWorld world;GameObject[] cars;double s;float speed=13;bool paused;
  TrackPoint At(double d){return track.Pose((d%track.Length+track.Length)%track.Length);}
  public void Initialize(MapWorld map){world=map;var prefab=Resources.Load<GameObject>("Train/SceneryCar");if(!prefab)return;var player=FindFirstObjectByType<Game>();if(map.info.routes==null||map.info.routes.Length<2)return;track=map.info.routes[1];var camera=Camera.main;double closest=0;float best=float.MaxValue;foreach(var p in track.points){float dist=Vector3.SqrMagnitude(new Vector3((float)p.x,(float)p.y,(float)p.z)-world.origin-camera.transform.position);if(dist<best){best=dist;closest=p.s;}}s=closest+600;cars=new GameObject[11];for(int i=0;i<cars.Length;i++)cars[i]=Instantiate(prefab,transform);UpdatePoses();}
  public void UseTrack(RouteData route){track=route;UpdatePoses();}
  public void SetPaused(bool value){paused=value;}
  void LateUpdate(){if(cars==null)return;if(!paused)s=(s+speed*Time.deltaTime)%track.Length;UpdatePoses();}
  public void VerifyPoses(){if(cars==null||cars.Length!=11)throw new System.Exception("Neighbor consist missing");double previous=s;foreach(double test in new[]{300d,1500d,2700d,track.Length-230}){s=test;UpdatePoses();for(int i=0;i<cars.Length;i++){var p=At(s-10-i*20);if(Vector3.Distance(cars[i].transform.position+world.origin,new Vector3((float)p.x,(float)p.y,(float)p.z))>.005f)throw new System.Exception("Neighbor track mismatch");}}s=previous;UpdatePoses();}
  void UpdatePoses(){if(cars==null)return;for(int i=0;i<cars.Length;i++){double center=s-10-i*20;var p=At(center);var a=At(center-7);var b=At(center+7);var direction=new Vector3((float)(b.x-a.x),(float)(b.y-a.y),(float)(b.z-a.z));cars[i].transform.SetPositionAndRotation(new Vector3((float)p.x,(float)p.y,(float)p.z)-world.origin,Quaternion.LookRotation(direction,Vector3.up));}}
 }
}
