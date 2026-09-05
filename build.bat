@echo off
cd /d D:\Descargas\MALPlus
"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" MALClient.Android\MALClient.Android.csproj /t:SignAndroidPackage /p:Configuration=Debug "/p:AndroidSdkDirectory=C:\Program Files (x86)\Android\android-sdk" /p:JavaSdkDirectory="C:\Program Files\OpenJDK\jdk-12.0.2" /p:EmbedAssembliesIntoApk=true /v:q /nologo /m