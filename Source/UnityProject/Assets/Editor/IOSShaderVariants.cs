using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Build;

using UnityEditor.Rendering;
// These effects are never enabled by the mobile runtime. Preserve ACES,
// HDR grading, all bloom quality modes, antialiasing and scaling variants.
public sealed class IOSShaderVariants : IPreprocessShaders {
 public int callbackOrder => 10000;
 static readonly HashSet<string> unused = new HashSet<string>{
  "_DISTORTION", "_CHROMATIC_ABERRATION", "_FILM_GRAIN",
  "_BLOOM_LQ_DIRT", "_BLOOM_HQ_DIRT", "DEBUG_DISPLAY", "SCREEN_COORD_OVERRIDE"
 };
 public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {
  if(EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) return;
  if(shader.name != "Hidden/Universal Render Pipeline/UberPost" && shader.name != "Hidden/Universal Render Pipeline/FinalPost") return;
  for(int i=data.Count-1;i>=0;i--){
   foreach(var keyword in data[i].shaderKeywordSet.GetShaderKeywords()) {
    if(unused.Contains(keyword.name)){data.RemoveAt(i);break;}
   }
  }
 }
}
