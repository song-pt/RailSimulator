Shader "RailDrivingSimulator/Atmosphere" {
Properties {_Noise("3D density noise",3D)="white"{}}
SubShader {Tags{"RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off ZTest Always
HLSLINCLUDE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE3D(_Noise);SAMPLER(sampler_Noise);TEXTURE2D(_RailVolume);SAMPLER(sampler_RailVolume);
float _RailMobile;float _RailQuality,_RailCloudCoverage,_RailFogDensity,_RailSunPower;float4 _RailSunScreen,_RailSunDirection,_RailSunColor,_RailOrigin;
float density(float3 p){float h=p.y;float shape=smoothstep(1100,1260,h)*(1-smoothstep(1590,1850,h));if(shape<.001)return 0;float3 uv=p*.00016+float3(_Time.y*.000018,0,_Time.y*.000009);float n=SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,uv,0).r*.58+SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,uv*2.07,0).r*.28+SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,uv*4.13,0).r*.14;return saturate((n-(1-_RailCloudCoverage))*.9)*shape;}
float3 ray(float2 uv){
#if UNITY_REVERSED_Z
float z=0.00001;
#else
float z=.99999;
#endif
return normalize(ComputeWorldSpacePosition(uv,z,UNITY_MATRIX_I_VP)-GetCameraPositionWS());}
float distanceScene(float2 uv){float d=SampleSceneDepth(uv);
#if UNITY_REVERSED_Z
if(d<.0000001)return 100000;
#else
if(d>.9999999)return 100000;
#endif
return distance(ComputeWorldSpacePosition(uv,d,UNITY_MATRIX_I_VP),GetCameraPositionWS());}
half4 Volume(Varyings v):SV_Target{float2 uv=v.texcoord;float3 rd=ray(uv);float3 eye=GetCameraPositionWS();float scene=distanceScene(uv);float3 rgb=0;float T=1;
if((_RailQuality>1.5||_RailMobile>.5)&&_RailSunPower>.001){float length=min(scene,300);int count=_RailMobile>.5?(_RailQuality>2.5?24:_RailQuality>1.5?16:8):(_RailQuality>2.5?48:32);float stepSize=length/count;float jitter=frac(sin(dot(uv*_ScreenParams.xy,float2(12.98,78.23)))*43758.54);float mu=dot(rd,_RailSunDirection.xyz);float g=.67;float phase=(1-g*g)/max(.12,pow(1+g*g-2*g*mu,1.5));for(int i=0;i<count;i++){float3 p=eye+rd*((i+jitter)*stepSize);float extinction=_RailFogDensity*exp(-max(0,p.y+_RailOrigin.y)/75);float atten=exp(-extinction*stepSize);float lit=MainLightRealtimeShadow(TransformWorldToShadowCoord(p));rgb+=T*(1-atten)*(float3(.35,.42,.49)*.40+_RailSunColor.rgb*lit*phase*.42*_RailSunPower);T*=atten;}}
if(rd.y>.012){float start=max(0,(1100-eye.y)/rd.y);float end=min(scene,(1850-eye.y)/rd.y);if(end>start){int count=_RailMobile>.5?(_RailQuality>2.5?32:_RailQuality>1.5?16:10):(_RailQuality>2.5?64:_RailQuality>1.5?32:20);float stepSize=(end-start)/count;for(int i=0;i<count;i++){float3 p=eye+rd*(start+(i+.5)*stepSize)+_RailOrigin.xyz;float den=density(p);if(den>.002){float lighting=exp(-(density(p+_RailSunDirection.xyz*80)+density(p+_RailSunDirection.xyz*180)+density(p+_RailSunDirection.xyz*320))*3.5);float a=1-exp(-den*stepSize*.022);float3 cloud=lerp(float3(.24,.28,.34),float3(.93,.95,1),lighting);cloud*=lerp(.09,1,saturate(_RailSunPower));rgb+=T*a*cloud;T*=1-a;if(T<.02)break;}}}}
return half4(rgb,T);}
half4 Composite(Varyings v):SV_Target{float2 uv=v.texcoord;half4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);half4 vol=SAMPLE_TEXTURE2D(_RailVolume,sampler_RailVolume,uv);float3 col=scene.rgb*vol.a+vol.rgb;
if(_RailSunScreen.z>0&&_RailSunPower>.05){float2 su=_RailSunScreen.xy;float visibility=0;if(all(su>0)&&all(su<1)){[unroll]for(int x=-2;x<=2;x++){[unroll]for(int y=-2;y<=2;y++){float2 q=su+float2(x,y)*float2(.0013,.0023);float dep=SampleSceneDepth(q);
#if UNITY_REVERSED_Z
visibility+=step(dep,.0000001);
#else
visibility+=step(.9999999,dep);
#endif
}}visibility/=25;visibility*=SAMPLE_TEXTURE2D(_RailVolume,sampler_RailVolume,su).a;}
float2 d=(uv-su)*float2(_ScreenParams.x/_ScreenParams.y,1);float r=length(d);float glow=exp(-r*r/0.0012)*.22+exp(-r*r/.032)*.013;float streak=exp(-abs(d.y)*160)*exp(-abs(d.x)*10)*.018;col+=_RailSunColor.rgb*(glow+streak)*visibility*_RailSunPower;}
return half4(col,scene.a);}
ENDHLSL
Pass{Name "ParticipatingMedium" HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Volume
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
ENDHLSL}
Pass{Name "OccludedGlare" HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Composite
ENDHLSL}
}}
