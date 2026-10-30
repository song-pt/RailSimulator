using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.EventSystems;
namespace RailDrivingSimulator {
 public static class MobileHaptics {
#if UNITY_IOS && !UNITY_EDITOR
 [System.Runtime.InteropServices.DllImport("__Internal")]static extern void RailSelectionHaptic();
#endif
 public static void Tick(){
#if UNITY_IOS && !UNITY_EDITOR
 RailSelectionHaptic();
#endif
 }
 }
 public static class MobileInput {
  static readonly HashSet<string> down=new HashSet<string>();static readonly Dictionary<string,HashSet<int>> held=new Dictionary<string,HashSet<int>>();
  public static void Press(string a){down.Add(a);}public static bool Down(string a){return down.Remove(a);}public static bool Held(string a){return held.TryGetValue(a,out var p)&&p.Count>0;}
  public static void Hold(string a,int pointer,bool on){if(!held.TryGetValue(a,out var p))held[a]=p=new HashSet<int>();if(on)p.Add(pointer);else p.Remove(pointer);}
  public static void Clear(){down.Clear();held.Clear();}
 }
 public class MobileHold:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler {
  public string action;public void OnPointerDown(PointerEventData e){MobileInput.Hold(action,e.pointerId,true);}public void OnPointerUp(PointerEventData e){MobileInput.Hold(action,e.pointerId,false);}public void OnPointerExit(PointerEventData e){MobileInput.Hold(action,e.pointerId,false);}void OnDisable(){MobileInput.Clear();}
 }
 public class MobileLook:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler {
  public Action<Vector2> turn;int pointer=int.MinValue;public void OnBeginDrag(PointerEventData e){if(pointer==int.MinValue)pointer=e.pointerId;}public void OnDrag(PointerEventData e){if(pointer==e.pointerId)turn?.Invoke(e.delta);}public void OnEndDrag(PointerEventData e){if(pointer==e.pointerId)pointer=int.MinValue;}
 }
}
