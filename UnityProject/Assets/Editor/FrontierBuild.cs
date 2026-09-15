using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier.Editor
{
    public static class FrontierBuild
    {
        const string Scene="Assets/Scenes/Frontier.unity";
        static string Root=>Directory.GetParent(Application.dataPath).Parent.FullName;
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            for(int i=0;i<Art.Palette.Length;i++)
            {
                string path="Assets/Resources/Materials/P"+i+".mat";
                Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                m.SetColor("_BaseColor",Art.Palette[i]);m.SetFloat("_Metallic",i==2?.6f:.15f);m.SetFloat("_Smoothness",i==12?.8f:.28f);m.enableInstancing=true;
                if(i==6||i==7||i==10){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Art.Palette[i]*2);}
                EditorUtility.SetDirty(m);
            }
            Material glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/EffectsGlow.mat");
            if(glow==null){glow=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(glow,"Assets/Resources/EffectsGlow.mat");}
            Material sky=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/FrontierSky.mat");
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,"Assets/Resources/FrontierSky.mat");}
            sky.SetColor("_SkyTint",new Color(.43f,.56f,.62f));sky.SetColor("_GroundColor",new Color(.26f,.24f,.22f));sky.SetFloat("_AtmosphereThickness",1.2f);sky.SetFloat("_Exposure",1.05f);EditorUtility.SetDirty(sky);
            foreach(string asset in new[]{"PC_RPAsset","Mobile_RPAsset"})
            {
                var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/"+asset+".asset");
                if(rp==null)continue;
                rp.msaaSampleCount=2;rp.renderScale=1;rp.supportsHDR=false;rp.shadowDistance=65;
                rp.mainLightShadowmapResolution=2048;
                var serialized=new SerializedObject(rp);serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=0;serialized.ApplyModifiedPropertiesWithoutUndo();
                rp.supportsCameraDepthTexture=false;rp.supportsCameraOpaqueTexture=false;
                EditorUtility.SetDirty(rp);
            }
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline=pipeline;
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i);QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;}
            QualitySettings.SetQualityLevel(1);
            PlayerSettings.companyName="Frontier Workshop";PlayerSettings.productName="Frontier Defense";
            PlayerSettings.bundleVersion="0.3.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.frontierworkshop.defense");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL,"com.frontierworkshop.defense.web");
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.template="PROJECT:Frontier";
            PlayerSettings.WebGL.initialMemorySize=256;PlayerSettings.WebGL.maximumMemorySize=2048;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.stripEngineCode=true;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Frontier Mission").AddComponent<GameWorld>();
            EditorSceneManager.SaveScene(scene,Scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("FRONTIER_PREPARED "+Application.unityVersion);
        }
        public static void BuildMac(){Build(BuildTarget.StandaloneOSX,Root+"/Builds/Mac/Frontier Defense.app");}
        public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,Root+"/Builds/Windows/Frontier Defense.exe");}
        public static void BuildAll(){BuildMac();BuildWindows();BuildWeb();}
        public static void BuildWeb()
        {
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;
            Build(BuildTarget.WebGL,Root+"/Builds/Web");
        }
        static void Build(BuildTarget target,string path)
        {
            PlayerSettings.bundleVersion="0.3.0";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=path,target=target,options=BuildOptions.None});
            var summary=result.summary;
            string json="{\"target\":\""+target+"\",\"result\":\""+summary.result+"\",\"bytes\":"+summary.totalSize+",\"errors\":"+summary.totalErrors+",\"warnings\":"+summary.totalWarnings+",\"seconds\":"+summary.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}";
            File.WriteAllText(Root+"/Verification/build-"+target+".json",json);Debug.Log("FRONTIER_BUILD "+json);
            if(summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+target);
        }
    }
}
