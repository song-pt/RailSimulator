using System;using System.Linq;using System.IO;using System.Collections;using System.Collections.Generic;using System.IO.Compression;using System.Security.Cryptography;using UnityEngine;
namespace RailDrivingSimulator {
 [Serializable] public class ChunkRecord {public string file;public float x,z;public uint crc;public string sha256;public bool landmark;}
 [Serializable] public class RouteRecord {public string id,file,sha256;}
 [Serializable] public class MapManifest {public int schema=1;public string id,name,platform,unityVersion,sourceFingerprint,routeSha256,shared,routeFile="route.json";public string trafficFile,trafficSHA256;public uint sharedCrc;public string sharedSha256;public ChunkRecord[] chunks;public RouteRecord[] routes;}
 public class MapInfo {public string folder;public MapManifest manifest;public RouteData route;public RouteData[] routes;public string Name{get{return manifest.name;}}}
 public static class Storage {
  static string cachedRoot;public static string Root{get{if(cachedRoot!=null)return cachedRoot;var s=Environment.GetEnvironmentVariable("RAILSIM_USER_DATA");if(!string.IsNullOrEmpty(s))return cachedRoot=s;
#if UNITY_STANDALONE_OSX
if(Directory.Exists("/Volumes/KINGSTON"))return cachedRoot="/Volumes/KINGSTON/RailDrivingSimulator/UserData";
#endif
return cachedRoot=Application.persistentDataPath;}}
  public static void Write(string name,string text){Directory.CreateDirectory(Root);string path=Path.Combine(Root,name),tmp=path+".tmp";File.WriteAllText(tmp,text);if(File.Exists(path)){File.Copy(path,path+".bak",true);File.Delete(path);}File.Move(tmp,path);}
  public static string Read(string name){string p=Path.Combine(Root,name);if(File.Exists(p))return File.ReadAllText(p);return null;}
  public static string SHA(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 }
 public static class MapCatalog {
  public static string Platform {get{
#if UNITY_IOS
return "iOS";
#else
return "StandaloneOSX";
#endif
}}
  public static List<MapInfo> Discover(){var result=new List<MapInfo>();foreach(var root in new[]{Path.Combine(Application.streamingAssetsPath,"Maps"),Path.Combine(Storage.Root,"Maps")}){if(!Directory.Exists(root))continue;foreach(var d in Directory.GetDirectories(root)){if(Path.GetFileName(d).StartsWith("_"))continue;try{result.Add(Read(d));}catch(Exception e){Debug.LogWarning(d+": "+e.Message);}}}return result;}
  public static string Safe(string folder,string name){if(string.IsNullOrEmpty(name)||Path.IsPathRooted(name)||name.Contains("..")||name.Contains("\\"))throw new InvalidDataException("地图引用路径无效");string p=Path.GetFullPath(Path.Combine(folder,name));if(!p.StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("地图引用超出包目录");return p;}
  public static MapInfo Read(string folder){string mf=Path.Combine(folder,"manifest.json");if(new FileInfo(mf).Length>4000000)throw new InvalidDataException("地图清单过大");var m=JsonUtility.FromJson<MapManifest>(File.ReadAllText(mf));if(m==null||m.schema!=1||m.platform!=Platform||string.IsNullOrWhiteSpace(m.id)||m.id.IndexOfAny(Path.GetInvalidFileNameChars())>=0||m.id.Contains("/")||m.id.Contains("..")||m.chunks==null||m.chunks.Length>5000)throw new InvalidDataException("地图格式、平台或ID不兼容");if(!File.Exists(Safe(folder,m.shared)))throw new FileNotFoundException("缺少共享材质包",m.shared);foreach(var c in m.chunks){if(!File.Exists(Safe(folder,c.file)))throw new FileNotFoundException("缺少地图分区",c.file);TrainSpec.Check(c.x,-1000000,1000000,"分区坐标");TrainSpec.Check(c.z,-1000000,1000000,"分区坐标");}string route=Safe(folder,m.routeFile);if(new FileInfo(route).Length>150000000)throw new InvalidDataException("线路数据过大");if(Storage.SHA(route)!=m.routeSha256)throw new InvalidDataException("线路数据校验失败");var r=JsonUtility.FromJson<RouteData>(File.ReadAllText(route));r.Validate();if(!string.IsNullOrEmpty(m.trafficFile)&&Storage.SHA(Safe(folder,m.trafficFile))!=m.trafficSHA256)throw new InvalidDataException("道路运行数据校验失败");var all=new List<RouteData>{r};if(m.routes!=null)foreach(var entry in m.routes){if(entry.id==r.id)continue;string f=Safe(folder,entry.file);if(new FileInfo(f).Length>150000000||Storage.SHA(f)!=entry.sha256)throw new InvalidDataException("方向线路校验失败");var additional=JsonUtility.FromJson<RouteData>(File.ReadAllText(f));additional.Validate();if(additional.id!=entry.id)throw new InvalidDataException("方向线路ID不匹配");all.Add(additional);}return new MapInfo{folder=folder,manifest=m,route=r,routes=all.ToArray()};}
  public static MapInfo Import(string file){var job=new MapImportJob(file,Path.Combine(Storage.Root,"Maps"));job.Extract(System.Threading.CancellationToken.None);return job.Commit();}
 }
 public class MapWorld:MonoBehaviour {
  [NonSerialized]public MapInfo info;public bool ready;public string error;public Vector3 origin;public float radius=2000;[NonSerialized]public bool mobileStreaming;
  AssetBundle shared;Dictionary<string,AssetBundle> bundles=new Dictionary<string,AssetBundle>();Dictionary<string,GameObject> tiles=new Dictionary<string,GameObject>();HashSet<string> pending=new HashSet<string>();Vector3 last;bool loading,disposed;AssetBundle loadingBundle;AsyncInstantiateOperation<GameObject> instantiating;float detailClock;List<MeshRenderer> details=new List<MeshRenderer>();
 void LateUpdate(){detailClock+=Time.unscaledDeltaTime;if(detailClock<.35f)return;detailClock=0;Vector3 p=last-origin;for(int i=details.Count-1;i>=0;i--){var r=details[i];if(!r){details.RemoveAt(i);continue;}float range=r.name.Contains("Ballast_Stone")?200:r.name.Contains("Fastening")?290:450;r.enabled=r.bounds.SqrDistance(p)<range*range;}}
  public IEnumerator Initialize(MapInfo map){info=map;mobileStreaming=Game.Mobile;if(mobileStreaming)radius=800;var req=AssetBundle.LoadFromFileAsync(MapCatalog.Safe(map.folder,map.manifest.shared),map.manifest.sharedCrc);req.completed+=_=>{if(disposed&&req.assetBundle)req.assetBundle.Unload(true);};yield return req;shared=req.assetBundle;if(!shared){error="共享资源加载失败";yield break;}shared.LoadAllAssets<Material>();ready=true;}
  public int VerifyPlacements(){int n=0;foreach(var c in info.manifest.chunks){if(!tiles.TryGetValue(c.file,out var tile))continue;if(Vector3.Distance(tile.transform.localPosition,new Vector3(c.x,0,c.z))>.001f)throw new Exception("Chunk floating-origin placement error");n++;}if(n==0)throw new Exception("No world chunks instantiated");return n;}
  public bool NeededReady(Vector3 world){foreach(var c in info.manifest.chunks)if(Required(c,world)&&!tiles.ContainsKey(c.file))return false;return true;}
  public void Follow(Vector3 world,Vector3 rebased){last=world;origin=rebased;transform.position=-origin;if(!loading)StartCoroutine(Stream());}
  public bool Required(ChunkRecord c,Vector3 p){float range=mobileStreaming?450:800;return Mathf.Abs(c.x+250-p.x)<range&&Mathf.Abs(c.z+250-p.z)<range;}
  float Distance(ChunkRecord c,Vector3 p){return Vector2.Distance(new Vector2(c.x+250,c.z+250),new Vector2(p.x,p.z));}
  public ChunkRecord[] Desired(Vector3 p){var nearby=info.manifest.chunks.Where(c=>Distance(c,p)<=(mobileStreaming?Mathf.Max(650,radius):radius)||(!mobileStreaming&&c.landmark&&Distance(c,p)<5000)).OrderBy(c=>Required(c,p)?0:1).ThenBy(c=>Distance(c,p));return mobileStreaming?nearby.Take(12).ToArray():nearby.ToArray();}
  IEnumerator Stream(){loading=true;
   var desired=Desired(last);var keep=new HashSet<string>(desired.Select(c=>c.file));var retired=new List<AssetBundle>();
   foreach(var key in tiles.Keys.ToArray())if(!keep.Contains(key)){var root=tiles[key];root.SetActive(false);Destroy(root);tiles.Remove(key);retired.Add(bundles[key]);bundles.Remove(key);}
   if(retired.Count>0){yield return null;foreach(var b in retired)if(b)b.Unload(true);details.RemoveAll(r=>!r);if(mobileStreaming)yield return Resources.UnloadUnusedAssets();}
   foreach(var c in desired){if(disposed)yield break;if(tiles.ContainsKey(c.file)||pending.Contains(c.file)||!Desired(last).Any(x=>x.file==c.file))continue;
    pending.Add(c.file);var req=AssetBundle.LoadFromFileAsync(MapCatalog.Safe(info.folder,c.file),c.crc);req.completed+=_=>{if(disposed&&req.assetBundle)req.assetBundle.Unload(true);};yield return req;if(disposed)yield break;
    var bundle=req.assetBundle;loadingBundle=bundle;if(!bundle){error="分区加载失败: "+c.file;pending.Remove(c.file);break;}
    var assetsRequest=bundle.LoadAllAssetsAsync<GameObject>();yield return assetsRequest;if(disposed)yield break;
    var asset=Array.ConvertAll(assetsRequest.allAssets,x=>x as GameObject);if(asset.Length!=1){error="分区须包含一个静态根对象";bundle.Unload(true);loadingBundle=null;pending.Remove(c.file);break;}
    var operation=UnityEngine.Object.InstantiateAsync(asset[0],transform);instantiating=operation;yield return operation;if(disposed)yield break;instantiating=null;
    var root=operation.Result[0];root.transform.localPosition=new Vector3(c.x,0,c.z);root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
    foreach(var component in root.GetComponentsInChildren<Component>(true))if(component&&!(component is Transform)&&!(component is MeshFilter)&&!(component is MeshRenderer)&&!(component is LODGroup))Destroy(component);
    foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(r.name.Contains("Ballast_Stone")||r.name.Contains("Fastening")||r.name.Contains("Rubber_Pad")||r.name.Contains("Sleeper"))details.Add(r);
    tiles[c.file]=root;bundles[c.file]=bundle;loadingBundle=null;pending.Remove(c.file);yield return null;
   }loading=false;
  }
  void OnDestroy(){disposed=true;if(instantiating!=null&&!instantiating.isDone)instantiating.Cancel();if(loadingBundle)loadingBundle.Unload(true);foreach(var b in bundles.Values)if(b)b.Unload(true);if(shared)shared.Unload(true);}
 }
}
