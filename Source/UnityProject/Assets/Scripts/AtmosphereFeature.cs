using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using UnityEngine.Rendering.RenderGraphModule;
namespace RailDrivingSimulator {
 public class AtmosphereFeature:ScriptableRendererFeature {
  public Material atmosphere;Pass pass;
  public override void Create(){pass=new Pass();pass.renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;pass.ConfigureInput(ScriptableRenderPassInput.Depth);}
  public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){if(atmosphere&&data.cameraData.cameraType==CameraType.Game&&Shader.GetGlobalFloat("_RailQuality")>=1&&Shader.GetGlobalFloat("_RailSunPower")>0){pass.mat=atmosphere;renderer.EnqueuePass(pass);}}
  class Pass:ScriptableRenderPass {
   public Material mat;class Data{public TextureHandle source;public Material mat;public int index;}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame){var r=frame.Get<UniversalResourceData>();if(r.isActiveTargetBackBuffer)return;var source=r.activeColorTexture;var desc=source.GetDescriptor(graph);desc.name="Half resolution participating medium";desc.width=Mathf.Max(1,desc.width/(Game.Mobile&&Shader.GetGlobalFloat("_RailQuality")<3?3:2));desc.height=Mathf.Max(1,desc.height/(Game.Mobile&&Shader.GetGlobalFloat("_RailQuality")<3?3:2));desc.depthBufferBits=DepthBits.None;desc.msaaSamples=MSAASamples.None;desc.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;desc.clearBuffer=false;var volume=graph.CreateTexture(desc);
    using(var b=graph.AddRasterRenderPass<Data>("Cloud density / shadowed light integration",out var d)){d.source=source;d.mat=mat;d.index=0;b.UseTexture(source,AccessFlags.Read);if(r.cameraDepthTexture.IsValid())b.UseTexture(r.cameraDepthTexture,AccessFlags.Read);if(r.mainShadowsTexture.IsValid())b.UseTexture(r.mainShadowsTexture,AccessFlags.Read);b.UseAllGlobalTextures(true);b.SetRenderAttachment(volume,0);b.SetGlobalTextureAfterPass(volume,Shader.PropertyToID("_RailVolume"));b.SetRenderFunc((Data data,RasterGraphContext ctx)=>Blitter.BlitTexture(ctx.cmd,data.source,new Vector4(1,1,0,0),data.mat,data.index));}
    var finalDesc=source.GetDescriptor(graph);finalDesc.name="Full resolution atmosphere composite";finalDesc.msaaSamples=MSAASamples.None;finalDesc.depthBufferBits=DepthBits.None;var final=graph.CreateTexture(finalDesc);
    using(var b=graph.AddRasterRenderPass<Data>("Depth occluded sun glare / composite",out var d)){d.source=source;d.mat=mat;d.index=1;b.UseTexture(source,AccessFlags.Read);b.UseTexture(volume,AccessFlags.Read);if(r.cameraDepthTexture.IsValid())b.UseTexture(r.cameraDepthTexture,AccessFlags.Read);b.UseAllGlobalTextures(true);b.SetRenderAttachment(final,0);b.SetRenderFunc((Data data,RasterGraphContext ctx)=>Blitter.BlitTexture(ctx.cmd,data.source,new Vector4(1,1,0,0),data.mat,data.index));}r.cameraColor=final;
   }
  }
 }
}
