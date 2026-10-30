using System;
using System.Collections.Generic;
namespace RailDrivingSimulator {
 public enum Difficulty { Normal, Hard, Realistic }
 public enum DoorPhase { Closed, Opening, Open, Closing }
 [Serializable] public class TrainSpec {
  public string name="E235 风格通勤列车";public double maxSpeedKmh=110,length=220,emergency=2.2,response=.65,coast=.025;
  public double[] power={0,.30,.55,.75,.95,1.15};public double[] brake={0,.30,.50,.70,.90,1.10,1.30,1.55,1.80};
  public void Validate(){Check(maxSpeedKmh,10,400,"最高速度");Check(length,1,400,"编组长度");Check(emergency,.1,8,"紧急制动");Check(response,.05,10,"响应");Check(coast,0,1,"惰行阻力");if(power==null||brake==null||power.Length<2||brake.Length<2||power.Length>16||brake.Length>16)throw new ArgumentException("档位数组须含零档和至少一个有效档，最多16档");for(int i=0;i<power.Length;i++)Check(power[i],0,5,"牵引档");for(int i=0;i<brake.Length;i++)Check(brake[i],0,8,"制动档");if(power[0]!=0||brake[0]!=0)throw new ArgumentException("零档必须为0");}
  public static void Check(double x,double min,double max,string label){if(double.IsNaN(x)||double.IsInfinity(x)||x<min||x>max)throw new ArgumentException(label+" 数值无效");}
  public TrainSpec Copy(){return new TrainSpec{name=name,maxSpeedKmh=maxSpeedKmh,length=length,emergency=emergency,response=response,coast=coast,power=(double[])power.Clone(),brake=(double[])brake.Clone()};}
 }
 [Serializable] public class TrackPoint {public double x,y,z,s;}
 [Serializable] public class Station {public string id,nameZh,nameJa,doorSide;public double stopS,platformStartS,platformEndS;public string Display{get{return nameZh==nameJa?nameZh:nameZh+" / "+nameJa;}}public double Target(double length){return Math.Max(platformStartS+length+.1,Math.Min(platformEndS-.1,stopS));}public bool Fits(double head,double length){return head>=platformStartS+length-.1&&head<=platformEndS+.1;}}
 [Serializable] public class RouteData {public string id,name;public bool loop;public double limitKmh=80,repeatLength;public TrackPoint[] points;public Station[] stations;
  public double Length{get{return points[points.Length-1].s;}}
  public void Validate(){if(points==null||points.Length<2||points.Length>2000000)throw new ArgumentException("线路点数量无效");if(points[0].s!=0)throw new ArgumentException("线路里程必须从0开始");double prev=-1;for(int i=0;i<points.Length;i++){var p=points[i];TrainSpec.Check(p.s,0,1000000,"里程");TrainSpec.Check(p.x,-1000000,1000000,"坐标");TrainSpec.Check(p.y,-1000000,1000000,"高程");TrainSpec.Check(p.z,-1000000,1000000,"坐标");if(p.s<=prev)throw new ArgumentException("里程必须递增");if(i>0){double d=Distance(points[i-1],p);if(d<.000001||d>1000||Math.Abs(d-(p.s-prev))>Math.Max(.05,d*.03))throw new ArgumentException("线路几何与里程不连续");}prev=p.s;}if(stations==null||stations.Length<2||stations.Length>1000)throw new ArgumentException("车站数量无效");prev=-1;var ids=new HashSet<string>();foreach(var st in stations){TrainSpec.Check(st.stopS,0,Length,"停车点");TrainSpec.Check(st.platformStartS,0,Length,"站台起点");TrainSpec.Check(st.platformEndS,0,Length,"站台终点");if(!ids.Add(st.id)||st.stopS<=prev||st.platformStartS<0||st.platformEndS>Length||st.platformStartS>=st.platformEndS||st.stopS<st.platformStartS||st.stopS>st.platformEndS||(st.doorSide!="left"&&st.doorSide!="right"))throw new ArgumentException("站点、停车点或开门侧无效");prev=st.stopS;}}
  static double Distance(TrackPoint a,TrackPoint b){return Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
  public TrackPoint Pose(double s){if(repeatLength>0)s=(s%repeatLength+repeatLength)%repeatLength;s=Math.Max(0,Math.Min(Length,s));int lo=0,hi=points.Length-1;while(hi-lo>1){int m=(lo+hi)/2;if(points[m].s<=s)lo=m;else hi=m;}var a=points[lo];var b=points[hi];double t=(s-a.s)/(b.s-a.s);return new TrackPoint{x=a.x+(b.x-a.x)*t,y=a.y+(b.y-a.y)*t,z=a.z+(b.z-a.z)*t,s=s};}
  public double Grade(double s){var a=Pose(s-2);var b=Pose(s+2);double d=Math.Sqrt((b.x-a.x)*(b.x-a.x)+(b.z-a.z)*(b.z-a.z));return d>.001?(b.y-a.y)/d:0;}
 }
 [Serializable] public class RunPlan {public string mapId,routeId;public int start,end;public bool compiledJourney;public int sourceStart,sourceEnd;public bool[] sourceSelected;public bool[] selected;public Difficulty difficulty;public string weather="晴",time="白天";public TrainSpec train=new TrainSpec();public void Validate(RouteData route){route.Validate();if(train==null||!Enum.IsDefined(typeof(Difficulty),difficulty))throw new ArgumentException("车辆或难度无效");train.Validate();if(start<0||end>=route.stations.Length||end<=start||selected==null||selected.Length!=route.stations.Length)throw new ArgumentException("交路必须按线路方向选择起终点");selected[start]=selected[end]=true;for(int i=start;i<=end;i++)if(selected[i]&&route.stations[i].platformEndS-route.stations[i].platformStartS<train.length+.1)throw new ArgumentException(route.stations[i].Display+" 的有效站台比编组短，请使用较短的车辆配置");}}
 [Serializable] public class RunState {public int schema=1;public string mapFingerprint;public RunPlan plan;public double s,v,a,seconds,distance;public int notch,next,reverser=1;public DoorPhase doors;public double doorFraction;public bool emergency,finished,initialized=true,wetSlip,lights=true,wiper,openedHere;public string doorSide="right";public List<string> served=new List<string>(),abandoned=new List<string>();}
 public class Simulation {
  public readonly RouteData route;public RunState state;public string Message="";public double TargetAcceleration{get;private set;}
  public Simulation(RouteData r,RunPlan p){p.train=p.train.Copy();p.Validate(r);route=r;state=new RunState{plan=p,s=r.stations[p.start].Target(p.train.length),next=p.start+1,initialized=p.difficulty!=Difficulty.Realistic,reverser=p.difficulty==Difficulty.Realistic?0:1};AdvanceSkipped();}
  public Station Next{get{return state.next<=state.plan.end?route.stations[state.next]:null;}}
  public bool InPlatform{get{if(Next==null)return false;double s=state.s;if(route.repeatLength>0){double laps=Math.Round((s-Next.Target(state.plan.train.length))/route.repeatLength);s-=laps*route.repeatLength;}return Next.Fits(s,state.plan.train.length);}}
  public double DistanceToStop{get{if(Next==null)return 0;double delta=Next.Target(state.plan.train.length)-state.s;if(route.repeatLength>0)delta-=Math.Round(delta/route.repeatLength)*route.repeatLength;return delta;}}
  public void ChangeNotch(int delta){if(state.finished)return;if(delta<0&&state.notch==1-state.plan.train.brake.Length)Emergency();state.notch=Math.Max(-state.plan.train.brake.Length+1,Math.Min(state.plan.train.power.Length-1,state.notch+delta));}
  public void Neutral(){state.notch=0;}
  public void Emergency(){state.emergency=true;Message="紧急制动：停稳、牵引回零后按复位键";}
  public bool ResetEmergency(){if(Math.Abs(state.v)>.05||state.notch>0){Message="复位需要停稳且牵引回零";return false;}state.emergency=false;state.v=state.a=TargetAcceleration=0;state.notch=0;Message="紧急制动已复位";return true;}
  public bool OpenDoor(){if(Math.Abs(state.v)>.05){Message="停稳后才能开门";return false;}if(state.plan.difficulty==Difficulty.Realistic&&Next!=null&&InPlatform&&state.doorSide!=Next.doorSide){Message="请确认正确开门侧";return false;}if(state.plan.difficulty!=Difficulty.Realistic&&Next!=null)state.doorSide=Next.doorSide;state.doors=DoorPhase.Opening;state.openedHere=InPlatform;Message=state.openedHere?"开门中；可随时按关门键，无最低停站时间":"已开门，但整列车不在目标站有效区域内";return true;}
  public void CloseDoor(){if(state.doors==DoorPhase.Closed)return;if(state.doorFraction<=0)state.openedHere=false;state.doors=DoorPhase.Closing;Message="关门中";}
  public bool AbandonNext(){if(Next==null||state.next==state.plan.end){Message="终点必须停靠";return false;}state.abandoned.Add(Next.id);state.next++;state.openedHere=false;AdvanceSkipped();Message="已由玩家确认放弃本次停靠";return true;}
  public void SetReverser(int dir){if(Math.Abs(state.v)>.05||state.notch!=0){Message="停稳并回到N后才能切换方向";return;}state.reverser=Math.Max(-1,Math.Min(1,dir));}
  void AdvanceSkipped(){while(state.next<state.plan.end&&!state.plan.selected[state.next])state.next++;}
  public void Tick(double dt,bool vigilanceHeld){if(dt<=0||dt>.1||double.IsNaN(dt))throw new ArgumentException("物理步长无效");if(state.finished)return;var t=state.plan.train;
   if(state.plan.difficulty!=Difficulty.Normal&&Math.Abs(state.v)>.05&&!vigilanceHeld)Emergency();
   if(state.doors==DoorPhase.Opening){state.doorFraction=Math.Min(1,state.doorFraction+dt/1.1);if(state.doorFraction>=1)state.doors=DoorPhase.Open;}
   if(state.doors==DoorPhase.Closing){state.doorFraction=Math.Max(0,state.doorFraction-dt/1.1);if(state.doorFraction<=0){state.doors=DoorPhase.Closed;if(state.openedHere&&InPlatform&&Math.Abs(state.v)<.05){state.served.Add(Next.id);state.openedHere=false;if(state.next==state.plan.end){state.finished=true;Message="交路完成";}else{state.next++;AdvanceSkipped();Message="停靠完成";}}}}
   if(state.finished){state.v=state.a=0;return;}
   double desired=0;int sign=state.reverser;if(state.emergency)desired=-t.emergency*Math.Sign(state.v==0?sign:state.v);else if(state.notch<0)desired=-t.brake[-state.notch]*Math.Sign(state.v==0?sign:state.v);else if(state.notch>0&&state.doors==DoorPhase.Closed&&state.initialized&&sign!=0&&Math.Abs(state.v)*3.6<t.maxSpeedKmh)desired=t.power[state.notch]*sign;
   state.wetSlip=state.plan.difficulty==Difficulty.Realistic&&state.plan.weather=="雨"&&Math.Abs(desired)>(state.notch>=0?.65:1.05);if(state.wetSlip)desired=Math.Sign(desired)*Math.Min(Math.Abs(desired),state.notch>=0?.65:1.05);
   if(Math.Abs(state.v)>.001)desired-=t.coast*Math.Sign(state.v);desired-=9.80665*route.Grade(state.s);TargetAcceleration=desired;
   state.a+=(desired-state.a)*Math.Min(1,dt/t.response);double old=state.v;double nv=state.v+state.a*dt;
   bool braking=state.emergency||state.notch<0||state.doors!=DoorPhase.Closed||state.notch==0||!state.initialized||sign==0;
   if(braking&&old*nv<=0)nv=0;
   if(Math.Abs(old)<.0001&&braking)nv=0;
   if(Math.Abs(nv)<.02&&braking)nv=0;
   if(braking&&nv==0){state.a=0;TargetAcceleration=0;}else state.a=(nv-old)/dt;
   double move=(old+nv)*.5*dt;state.s+=move;state.distance+=Math.Abs(move);state.v=nv;state.seconds+=dt;
   if(route.repeatLength<=0&&(state.s<0||state.s>route.Length)){state.s=Math.Max(0,Math.Min(route.Length,state.s));state.v=state.a=0;Message="已到地图边界；未完成停站仍保留，请重置进站位置";}
  }
  public void ResetApproach(){if(Next==null)return;state.s=Math.Max(route.stations[state.plan.start].Target(state.plan.train.length),Next.platformStartS-350);state.v=state.a=0;state.notch=0;state.emergency=false;state.doors=DoorPhase.Closed;state.doorFraction=0;state.openedHere=false;}
  public void Restore(RunState saved){if(saved==null||saved.schema!=1||saved.plan==null)throw new ArgumentException("存档格式无效");saved.plan.Validate(route);TrainSpec.Check(saved.s,route.repeatLength>0?-1000000000:0,route.repeatLength>0?1000000000:route.Length,"存档位置");TrainSpec.Check(saved.v,-120,120,"存档速度");TrainSpec.Check(saved.a,-20,20,"存档加速度");TrainSpec.Check(saved.doorFraction,0,1,"存档门状态");TrainSpec.Check(saved.seconds,0,100000000,"存档运行时间");TrainSpec.Check(saved.distance,0,1000000000,"存档运行距离");if(saved.served==null||saved.abandoned==null||saved.reverser< -1||saved.reverser>1)throw new ArgumentException("存档附加状态无效");if(saved.next<=saved.plan.start||saved.next>saved.plan.end+1||saved.notch<=-saved.plan.train.brake.Length||saved.notch>=saved.plan.train.power.Length||!Enum.IsDefined(typeof(DoorPhase),saved.doors))throw new ArgumentException("存档状态无效");state=saved;}
 }
}
