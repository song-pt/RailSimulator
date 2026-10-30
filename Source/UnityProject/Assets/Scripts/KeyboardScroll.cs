using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;
namespace RailDrivingSimulator {
 public class KeyboardScroll:MonoBehaviour {
  ScrollRect scroll;GameObject previous;
  void Awake(){scroll=GetComponent<ScrollRect>();}
  void LateUpdate(){if(!EventSystem.current)return;var selected=EventSystem.current.currentSelectedGameObject;if(!selected||selected==previous||!selected.transform.IsChildOf(scroll.content))return;previous=selected;Canvas.ForceUpdateCanvases();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,selected.transform);var viewport=scroll.viewport.rect;float change=bounds.min.y<viewport.yMin?viewport.yMin-bounds.min.y:bounds.max.y>viewport.yMax?viewport.yMax-bounds.max.y:0;if(change!=0){var p=scroll.content.anchoredPosition;p.y+=change;scroll.content.anchoredPosition=p;}}
 }
}
