1. First, check that you have the [necessary tools](#requirements) installed.
2. Go to <https://my.telegram.org/apps> and register a new app.
3. Clone the repository __*recursively*__ `git clone --recursive https://github.com/UnigramDev/Unigram.git`.
4. Create a new file inside `Unigram/Unigram/Unigram` and name it `Constants.Secret.cs`:
```csharp
namespace Unigram
{
    public static partial class Constants
    {
        static Constants()
        {
            ApiId = your_api_id;
            ApiHash = "your_api_hash";
        }
    }
}
```
5. Replace `your_api_id` and `your_api_hash` with the data obtained from step 2.

## Requirements
The following tools and SDKs are mandatory for the project development:
* Visual Studio 2017/2019, with
    * .NET Native
    * .NET Framework 4.7 SDK
    * NuGet package manager
    * Universal Windows Platform tools
    * Windows 10 SDK 17134
    * [TDLib for Universal Windows Platform](https://tdlib.github.io/td/build.html?language=C%23)

## Dependencies
The app uses the following NuGet packages to work:
* [Autofac](https://www.nuget.org/packages/Autofac/)
* [HockeySDK.UWP](https://www.nuget.org/packages/HockeySDK.UWP/)
* [Microsoft.NETCore.UniversalWindowsPlatform](https://www.nuget.org/packages/Microsoft.NETCore.UniversalWindowsPlatform/)
* [Microsoft.Xaml.Behaviors.Uwp.Managed](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Uwp.Managed/)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
* [System.Reactive](https://www.nuget.org/packages/System.Reactive/)
* [Win2D.uwp](https://www.nuget.org/packages/Win2D.uwp/)

The project also relies on `libogg`, `libopus`, `libopusfile`, `libwebp` and `libtgvoip` that are included in the repository.

Few additional dependencies should be installed via vcpkg.

### Installing additional dependencies via vcpkg

1. Create a folder which will store `vcpkg` tool and a few GB of data for installed packages.
2. Run the following commands inside of `cmd.exe` console inside of newly created empty folder:

```
git clone https://github.com/Microsoft/vcpkg.git .
REM Switch to the same commit as tdlib build instructions suggest:
git checkout 1b1ae50e1a69f7c659bd7d731e80b358d21c86ad
bootstrap-vcpkg.bat

REM Edit config .\ports\ffmpeg\portfile.cmake to get custom compile options with libvpx:
powershell -command "((Get-Content -path .\ports\ffmpeg\portfile.cmake -Raw) -replace '{OPTIONS} --enable-libvpx','{OPTIONS} --disable-everything --enable-hwaccel=h264_d3d11va --enable-hwaccel=h264_d3d11va2 --enable-hwaccel=h264_dxva2 --enable-hwaccel=hevc_d3d11va --enable-hwaccel=hevc_d3d11va2 --enable-hwaccel=hevc_dxva2 --enable-hwaccel=mpeg2_d3d11va --enable-hwaccel=mpeg2_d3d11va2 --enable-hwaccel=mpeg2_dxva2 --enable-protocol=file --enable-libopus --enable-libvpx --enable-decoder=aac --enable-decoder=aac_fixed --enable-decoder=aac_latm --enable-decoder=aasc --enable-decoder=alac --enable-decoder=flac --enable-decoder=gif --enable-decoder=h264 --enable-decoder=hevc --enable-decoder=libvpx_vp8 --enable-decoder=libvpx_vp9 --enable-decoder=mp1 --enable-decoder=mp1float --enable-decoder=mp2 --enable-decoder=mp2float --enable-decoder=mp3 --enable-decoder=mp3adu --enable-decoder=mp3adufloat --enable-decoder=mp3float --enable-decoder=mp3on4 --enable-decoder=mp3on4float --enable-decoder=mpeg4 --enable-decoder=msmpeg4v2 --enable-decoder=msmpeg4v3 --enable-decoder=opus --enable-decoder=pcm_alaw --enable-decoder=pcm_f32be --enable-decoder=pcm_f32le --enable-decoder=pcm_f64be --enable-decoder=pcm_f64le --enable-decoder=pcm_lxf --enable-decoder=pcm_mulaw --enable-decoder=pcm_s16be --enable-decoder=pcm_s16be_planar --enable-decoder=pcm_s16le --enable-decoder=pcm_s16le_planar --enable-decoder=pcm_s24be --enable-decoder=pcm_s24daud --enable-decoder=pcm_s24le --enable-decoder=pcm_s24le_planar --enable-decoder=pcm_s32be --enable-decoder=pcm_s32le --enable-decoder=pcm_s32le_planar --enable-decoder=pcm_s64be --enable-decoder=pcm_s64le --enable-decoder=pcm_s8 --enable-decoder=pcm_s8_planar --enable-decoder=pcm_u16be --enable-decoder=pcm_u16le --enable-decoder=pcm_u24be --enable-decoder=pcm_u24le --enable-decoder=pcm_u32be --enable-decoder=pcm_u32le --enable-decoder=pcm_u8 --enable-decoder=vorbis --enable-decoder=wavpack --enable-decoder=wmalossless --enable-decoder=wmapro --enable-decoder=wmav1 --enable-decoder=wmav2 --enable-decoder=wmavoice --enable-encoder=libopus --enable-parser=aac --enable-parser=aac_latm --enable-parser=flac --enable-parser=h264 --enable-parser=hevc --enable-parser=mpeg4video --enable-parser=mpegaudio --enable-parser=opus --enable-parser=vorbis --enable-demuxer=aac --enable-demuxer=flac --enable-demuxer=gif --enable-demuxer=h264 --enable-demuxer=hevc --enable-demuxer=matroska --enable-demuxer=m4v --enable-demuxer=mov --enable-demuxer=mp3 --enable-demuxer=ogg --enable-demuxer=wav --enable-muxer=ogg --enable-muxer=opus') | Set-Content -Path .\ports\ffmpeg\portfile.cmake"

vcpkg install ffmpeg[opus,vpx]:x64-uwp zlib:x64-uwp lz4:x64-uwp

REM Register vcpkg installation globally for automatic package detection by MSBuild:
vcpkg integrate install
```
