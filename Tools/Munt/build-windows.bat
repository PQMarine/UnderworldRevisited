@echo off
rem Builds Munt's libmt32emu (LGPL 2.1 or later) UNMODIFIED as mt32emu.dll for Windows x64 -
rem the library UWMt32MusicDriver calls. The source is ThirdParty\Munt\munt-src-6e7c01f.zip
rem (github.com/munt/munt, commit 6e7c01f, libmt32emu 2.8.3); unpack it and pass its mt32emu
rem folder:
rem
rem   build-windows.bat <path to munt-6e7c01f\mt32emu> <output folder>
rem
rem Settings as Munt's CMake script writes them for a shared build with the C interface only
rem (config.h beside this file), the internal resampler and the static C runtime, without CMake.
rem Needs Visual Studio 2022 with the C++ tools.
setlocal
if "%~2"=="" (
  echo build-windows.bat ^<munt mt32emu folder^> ^<output folder^>
  exit /b 1
)
set SRC=%~1\src
set OUT=%~f2
set GEN=%~dp0
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if not exist "%OUT%\obj" mkdir "%OUT%\obj"
cd /d "%OUT%\obj"
cl /nologo /c /O2 /EHsc /MT /W3 /std:c++14 ^
  /DMT32EMU_WITH_INTERNAL_RESAMPLER /Dmt32emu_EXPORTS /D_CRT_SECURE_CPP_OVERLOAD_STANDARD_NAMES=1 ^
  /I"%GEN%." /I"%SRC%" ^
  "%SRC%\Analog.cpp" "%SRC%\BReverbModel.cpp" "%SRC%\Display.cpp" "%SRC%\File.cpp" "%SRC%\FileStream.cpp" ^
  "%SRC%\LA32FloatWaveGenerator.cpp" "%SRC%\LA32Ramp.cpp" "%SRC%\LA32WaveGenerator.cpp" "%SRC%\MidiStreamParser.cpp" ^
  "%SRC%\Part.cpp" "%SRC%\Partial.cpp" "%SRC%\PartialManager.cpp" "%SRC%\Poly.cpp" "%SRC%\ROMInfo.cpp" ^
  "%SRC%\Synth.cpp" "%SRC%\Tables.cpp" "%SRC%\TVA.cpp" "%SRC%\TVF.cpp" "%SRC%\TVP.cpp" "%SRC%\sha1\sha1.cpp" ^
  "%SRC%\SampleRateConverter.cpp" "%SRC%\c_interface\c_interface.cpp" ^
  "%SRC%\srchelper\srctools\src\FIRResampler.cpp" "%SRC%\srchelper\srctools\src\SincResampler.cpp" ^
  "%SRC%\srchelper\srctools\src\IIR2xResampler.cpp" "%SRC%\srchelper\srctools\src\LinearResampler.cpp" ^
  "%SRC%\srchelper\srctools\src\ResamplerModel.cpp" "%SRC%\srchelper\InternalResampler.cpp"
if errorlevel 1 exit /b 1
link /nologo /DLL /OUT:"%OUT%\mt32emu.dll" *.obj
if errorlevel 1 exit /b 1
echo built %OUT%\mt32emu.dll
