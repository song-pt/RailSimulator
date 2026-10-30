using System;using System.Collections.Generic;
namespace RailDrivingSimulator {
 public static class JourneyPlanner {
  public static int[] Order(RouteData source,int from,int to){if(from<0||to<0||from>=source.stations.Length||to>=source.stations.Length)throw new ArgumentException("起终站无效");int count=source.loop?(to-from+source.stations.Length)%source.stations.Length:to-from;if(source.loop&&count==0)count=source.stations.Length;if(count<=0)throw new ArgumentException("交路方向无效");var order=new int[count+1];for(int i=0;i<order.Length;i++)order[i]=(from+i)%source.stations.Length;return order;}
  static TrackPoint WrapPose(RouteData r,double s){double x=s%r.Length;if(x<0)x+=r.Length;return r.Pose(x);}
  public static RouteData Compile(RouteData source,RunPlan request,out RunPlan plan){
   if(!source.loop){plan=request;return source;}
   source.Validate();int from=request.compiledJourney?request.sourceStart:request.start,to=request.compiledJourney?request.sourceEnd:request.end;bool[] selection=request.compiledJourney?request.sourceSelected:request.selected;if(selection==null||selection.Length!=source.stations.Length)throw new ArgumentException("停站选择无效");var order=Order(source,from,to);double offset=source.stations[from].platformStartS;var stations=new List<Station>();double previous=-1;
   for(int j=0;j<order.Length;j++){var st=source.stations[order[j]];double a=st.platformStartS-offset;while(a<=previous)a+=source.Length;stations.Add(new Station{id=st.id+"@"+j,nameZh=st.nameZh,nameJa=st.nameJa,doorSide=st.doorSide,platformStartS=a,platformEndS=a+(st.platformEndS-st.platformStartS),stopS=a+(st.stopS-st.platformStartS)});previous=a;}
   double end=Math.Max(source.Length+300,stations[stations.Count-1].platformEndS+75);var cuts=new SortedSet<double>{0,end};for(int lap=-1;lap<=2;lap++)foreach(var point in source.points){double s=point.s+lap*source.Length-offset;if(s>0&&s<end)cuts.Add(s);}
   var points=new List<TrackPoint>();double chain=0;TrackPoint last=null;foreach(double s in cuts){var p=WrapPose(source,offset+s);if(last!=null){double d=Math.Sqrt(Math.Pow(p.x-last.x,2)+Math.Pow(p.y-last.y,2)+Math.Pow(p.z-last.z,2));if(d<.000001)continue;chain+=d;}p=new TrackPoint{x=p.x,y=p.y,z=p.z,s=chain};points.Add(p);last=p;}
   var selected=new bool[order.Length];for(int j=0;j<order.Length;j++)selected[j]=j==0||j==order.Length-1||selection[order[j]];
   plan=new RunPlan{mapId=request.mapId,routeId=source.id,start=0,end=order.Length-1,selected=selected,difficulty=request.difficulty,weather=request.weather,time=request.time,train=request.train.Copy(),compiledJourney=true,sourceStart=from,sourceEnd=to,sourceSelected=(bool[])selection.Clone()};
   var route=new RouteData{id=source.id+"_journey",name=source.name,loop=false,limitKmh=source.limitKmh,repeatLength=source.Length,points=points.ToArray(),stations=stations.ToArray()};plan.Validate(route);return route;
  }
 }
}
