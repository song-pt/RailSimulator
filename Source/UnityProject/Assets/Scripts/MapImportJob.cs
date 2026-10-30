using System;using System.IO;using System.IO.Compression;using System.Security.Cryptography;using System.Collections.Generic;using System.Threading;using UnityEngine;
namespace RailDrivingSimulator {
 public sealed class MapImportJob {
  public volatile bool Paused;readonly string platform;readonly long freeBytes;readonly string file,root;readonly MapManifest header;readonly Dictionary<string,string> hashes=new Dictionary<string,string>(StringComparer.Ordinal);readonly object gate=new object();string stage="准备读取",name="";long bytes,total;string staged;
  public MapImportJob(string file,string root){this.file=file;this.root=root;platform=MapCatalog.Platform;freeBytes=long.MaxValue;
#if UNITY_IOS && !UNITY_EDITOR
freeBytes=AvailableBytes();
#endif
if(!File.Exists(file)||new FileInfo(file).Length>17179869184)throw new IOException("请选择存在的 .railmap 文件（最大16GB）");using(var z=ZipFile.OpenRead(file)){var e=z.GetEntry("manifest.json");if(e==null||e.Length>4000000)throw new InvalidDataException("地图清单缺失或过大");using(var sr=new StreamReader(e.Open()))header=JsonUtility.FromJson<MapManifest>(sr.ReadToEnd());}if(header==null||header.schema!=1||header.platform!=platform||string.IsNullOrWhiteSpace(header.id)||header.id.Contains("/")||header.id.Contains("\\")||header.id.Contains("..")||header.chunks==null||header.chunks.Length>5000)throw new InvalidDataException("地图格式、平台或ID无效");Add(header.shared,header.sharedSha256);Add(header.routeFile,header.routeSha256);if(header.routes!=null)foreach(var r in header.routes)Add(r.file,r.sha256);if(!string.IsNullOrEmpty(header.trafficFile))Add(header.trafficFile,header.trafficSHA256);foreach(var c in header.chunks)Add(c.file,c.sha256);}
  [System.Runtime.InteropServices.DllImport("__Internal",EntryPoint="RailAvailableBytes")]static extern long AvailableBytes();
  [System.Runtime.InteropServices.DllImport("__Internal",EntryPoint="RailExcludeBackup")]static extern void ExcludeBackup(string path);
  void CheckCancel(CancellationToken token){token.ThrowIfCancellationRequested();while(Paused){token.ThrowIfCancellationRequested();Thread.Sleep(100);}}
  void Add(string p,string h){MapCatalog.Safe(root,p);if(string.IsNullOrEmpty(h)||h.Length!=64)throw new InvalidDataException("资源校验值无效");if(hashes.TryGetValue(p,out var old)&&!string.Equals(old,h,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("资源校验值冲突");hashes[p]=h;}
  public string Progress(){lock(gate)return stage+"\n"+(total>0?(100.0*bytes/total).ToString("F1")+"%  "+(bytes/1048576.0).ToString("F0")+" / "+(total/1048576.0).ToString("F0")+" MB":"读取目录")+"\n"+name;}
  // Background phase: pure IO and crypto only; never calls Unity JSON/API or map discovery.
  public void Extract(CancellationToken token){Directory.CreateDirectory(root);staged=Path.Combine(root,"_import_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staged);try{using(var z=ZipFile.OpenRead(file)){if(z.Entries.Count>10000)throw new InvalidDataException("地图条目过多");long declared=0;var names=new HashSet<string>(StringComparer.Ordinal);foreach(var e in z.Entries){if(e.Length>4294967296)throw new InvalidDataException("资源文件过大");declared=checked(declared+e.Length);if(declared>34359738368)throw new InvalidDataException("地图展开尺寸过大");if(!names.Add(e.FullName))throw new InvalidDataException("重复资源文件");MapCatalog.Safe(staged,e.FullName);}if(declared>freeBytes-268435456)throw new IOException("手机剩余空间不足，请清理后再导入");foreach(var p in hashes.Keys)if(!names.Contains(p))throw new InvalidDataException("缺少地图资源："+p);lock(gate){total=declared;stage="解包并校验（可取消）";}byte[] buffer=new byte[1024*1024];foreach(var e in z.Entries){CheckCancel(token);string p=MapCatalog.Safe(staged,e.FullName);if(e.FullName.EndsWith("/")){Directory.CreateDirectory(p);continue;}string ext=Path.GetExtension(p).ToLowerInvariant();if(ext!=".json"&&ext!=".bundle"&&ext!=".txt")throw new InvalidDataException("地图包含未支持的资源类型");lock(gate)name=e.FullName;Directory.CreateDirectory(Path.GetDirectoryName(p));long count=0;using(var input=e.Open())using(var output=new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,FileOptions.SequentialScan))using(var hash=SHA256.Create()){int n;while((n=input.Read(buffer,0,buffer.Length))>0){CheckCancel(token);count+=n;if(count>e.Length)throw new InvalidDataException("资源实际长度异常");output.Write(buffer,0,n);hash.TransformBlock(buffer,0,n,buffer,0);lock(gate)bytes+=n;}hash.TransformFinalBlock(buffer,0,0);if(count!=e.Length)throw new InvalidDataException("资源长度不完整");if(hashes.TryGetValue(e.FullName,out var expected)&&!string.Equals(BitConverter.ToString(hash.Hash).Replace("-", ""),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("资源校验失败："+e.FullName);}}}CheckCancel(token);lock(gate){stage="解包完成，等待线路校验";name="";}}catch{Cleanup();throw;}}
  public void Cleanup(){if(staged!=null&&Directory.Exists(staged))try{Directory.Delete(staged,true);}catch{} }
  // Main-thread commit: Unity JSON/route validation and atomic folder installation.
  public MapInfo Commit(){try{var map=MapCatalog.Read(staged);string dest=Path.Combine(root,header.id),backup=null;if(Directory.Exists(dest)){backup=Path.Combine(root,"_backup_"+header.id+"_"+Guid.NewGuid().ToString("N"));Directory.Move(dest,backup);}try{Directory.Move(staged,dest);}catch{if(backup!=null&&!Directory.Exists(dest))Directory.Move(backup,dest);throw;}map.folder=dest;
#if UNITY_IOS && !UNITY_EDITOR
ExcludeBackup(dest);
#endif
if(platform=="iOS"&&backup!=null)try{Directory.Delete(backup,true);}catch{}return map;}catch{Cleanup();throw;}}
 }
 public static class NativeMapPicker {
  [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.B.dylib")]static extern IntPtr dlopen(string file,int flags);
  [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.B.dylib")]static extern IntPtr dlsym(IntPtr handle,string name);
  [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]delegate IntPtr Pick();static IntPtr handle;
  public static string Choose(){
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
   if(handle==IntPtr.Zero)handle=dlopen(Path.Combine(Application.dataPath,"Plugins/libRailFilePicker.dylib"),2);if(handle==IntPtr.Zero)throw new IOException("文件选择组件缺失");var symbol=dlsym(handle,"RailChooseMap");if(symbol==IntPtr.Zero)throw new IOException("文件选择接口缺失");var fn=(Pick)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(symbol,typeof(Pick));var p=fn();return p==IntPtr.Zero?null:System.Runtime.InteropServices.Marshal.PtrToStringAnsi(p);
#else
   throw new IOException("原生文件窗口在 macOS 游戏版中可用");
#endif
  }
 }
}
