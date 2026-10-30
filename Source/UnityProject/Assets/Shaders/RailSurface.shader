Shader "RailDrivingSimulator/RailSurface" {
 Properties {
 _BaseMap("Diffuse",2D)="white"{} _NormalMap("Normal",2D)="bump"{} _RoughMap("Roughness",2D)="white"{}
 _HasNormal("Normal enabled",Float)=0 _Matte("Matte masonry",Float)=0
 _BaseColor("Tint",Color)=(1,1,1,1) _Roughness("Roughness",Float)=.7 _Metallic("Metallic",Float)=0 _UVScale("Texture repeat",Float)=1 _GradeMix("Tint mix",Float)=0 _Windows("Window style (-1 none)",Float)=-1 _Office("Office grid",Float)=0 _Ground("Wet ground",Float)=0
 }
 SubShader {Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
 Pass {Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
 #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);TEXTURE2D(_RoughMap);SAMPLER(sampler_RoughMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _HasNormal,_Roughness,_Metallic,_UVScale,_GradeMix,_Windows,_Office,_Ground,_Matte;
 CBUFFER_END
 float _RailWetness,_RailNight;
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 grid:TEXCOORD1;};
 struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float2 grid:TEXCOORD3;float fog:TEXCOORD4;};
 V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv*_UVScale;o.grid=a.grid;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 half4 frag(V v):SV_Target {
 float3 col=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb;col=lerp(col,_BaseColor.rgb,saturate(_GradeMix));if(_GradeMix<.001)col*=_BaseColor.rgb;
 float rough=SAMPLE_TEXTURE2D(_RoughMap,sampler_RoughMap,v.uv).r*_Roughness;
 if(_Matte>.5)rough=max(rough,.9);float3 n=normalize(v.normalWS);if(_HasNormal>.5){float3 dp1=ddx(v.positionWS),dp2=ddy(v.positionWS);float2 du1=ddx(v.uv),du2=ddy(v.uv);if(abs(du1.x*du2.y-du1.y*du2.x)>1e-8){float3 t=normalize(dp1*du2.y-dp2*du1.y);float3 b=normalize(-dp1*du2.x+dp2*du1.x);float signUV=sign(du1.x*du2.y-du1.y*du2.x);t*=signUV;b*=signUV;float3 mapped=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,v.uv).rgb*2-1;n=normalize(t*mapped.x+b*mapped.y+n*mapped.z);}}
 float window=0;if(_Windows>=0&&abs(v.normalWS.y)<.65){float2 grid=frac(v.grid);float w=.30+fmod(_Windows,3)*.055;window=step(w,grid.x)*step(grid.x,1-w)*step(.22,grid.y)*step(grid.y,.74);}
 if(_Office>.5&&abs(v.normalWS.y)<.65){float2 g=frac(v.uv/float2(1.8,3.5));window=step(.065,g.x)*step(g.x,.94)*step(.055,g.y)*step(g.y,.91);col=float3(.38,.4,.42);}
 col=lerp(col,float3(.075,.13,.17)+.025*sin(floor(v.grid.x)*2.13+floor(v.grid.y)*4.21),window);rough=lerp(rough,.21,window);rough=lerp(rough,.10,saturate(_RailWetness*_Ground));col*=1-_RailWetness*_Ground*.18;
 InputData input=(InputData)0;input.positionWS=v.positionWS;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);input.shadowCoord=TransformWorldToShadowCoord(v.positionWS);input.bakedGI=SampleSH(n);input.fogCoord=v.fog;input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);input.shadowMask=half4(1,1,1,1);
 SurfaceData s=(SurfaceData)0;s.albedo=col;s.metallic=_Metallic;s.specular=half3(.04,.04,.04);s.smoothness=1-saturate(rough);s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;s.emission=window*_RailNight*float3(.27,.23,.15)*step(.48,frac(sin(dot(floor(v.grid),float2(12.98,78.2)))*43758.5));half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,v.fog);return color;
 }
 ENDHLSL }
 Pass {Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull Back
 HLSLPROGRAM
 #pragma vertex shadowVert
 #pragma fragment shadowFrag
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
 float3 _LightDirection;float3 _LightPosition;
 struct SA{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 float4 shadowVert(SA a):SV_POSITION{float3 p=TransformObjectToWorld(a.positionOS.xyz);float3 n=TransformObjectToWorldNormal(a.normalOS);
 #if _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 direction=normalize(_LightPosition-p);
 #else
 float3 direction=_LightDirection;
 #endif
 float4 c=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
 #if UNITY_REVERSED_Z
 c.z=min(c.z,UNITY_NEAR_CLIP_VALUE);
 #else
 c.z=max(c.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return c;}
 half4 shadowFrag():SV_Target{return 0;}
 ENDHLSL }
 Pass {Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask 0 Cull Back
 HLSLPROGRAM
 #pragma vertex depthVert
 #pragma fragment depthFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 depthVert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
 half4 depthFrag():SV_Target{return 0;}
 ENDHLSL }
 }
}
